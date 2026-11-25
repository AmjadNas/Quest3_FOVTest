using System.Collections;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FOVTest.Tests
{
    public class SimulatedRunTests
    {
        GameObject go;

        [SetUp]
        public void Frames()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 72;
        }

        [TearDown]
        public void Clean()
        {
            if (go != null) Object.DestroyImmediate(go);
            for (var s = GameObject.Find("Stage"); s != null; s = GameObject.Find("Stage")) Object.DestroyImmediate(s);
        }

        TestSession Make(out SimulatedParticipant head, out Stage stage, out TestLayout layout)
        {
            go = new GameObject("sim");
            var cam = go.AddComponent<Camera>();
            head = go.AddComponent<SimulatedParticipant>();
            var settings = new AppSettings();
            settings.Patient.UserID = 12345;
            settings.Test.MaxDroppedFrames = 1000;      // editor frame times are not the headset's
            settings.Test.InterTrialSec = 0.1f;
            var lum = LuminanceModel.Quest3Nominal();
            stage = new Stage(cam, lum, settings.Test, Shader.Find("FOVTest/Spot"));
            layout = TestLayout.LoadBuiltin("Training-30deg");
            stage.Configure(layout.Meta, 2f);
            var session = new TestSession(settings, layout, stage, head, lum);
            head.Stage = stage;
            head.Test = session.Test;
            session.Test.SetPrimaryToHead();
            return session;
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator FullRun_ProducesACompleteLog()
        {
            var session = Make(out var head, out _, out var layout);
            // One location answered in the wrong direction every time, one never answered.
            head.Strategy = s => s.IsBlindspot ? "trigger" : s.LocationId == 5005 ? "wrong" : s.LocationId == -5002 ? "none" : "seen";
            yield return session.Test.Run(layout.Select("Bilateral", true), 7);
            var err = session.Save();
            Assert.IsNull(err, err);

            var bytes = File.ReadAllBytes(session.LogPath);
            Assert.AreEqual(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3).ToArray(), "BOM");
            var log = JObject.Parse(File.ReadAllText(session.LogPath));
            CheckExtra(session, log);
            var fixture = Path.Combine(Application.dataPath, "FOVTest/Tests/Editor/Fixtures/vendor_history_summary.txt");
            if (!File.Exists(fixture)) Assert.Ignore("The reference fixture is not available.");
            var reference = JObject.Parse(File.ReadAllText(fixture));
            TestContext.WriteLine("LOG " + session.LogPath);

            foreach (var section in new[] { "Summary" })
                CollectionAssert.AreEqual(((JObject)reference[section]).Properties().Select(p => p.Name),
                                          ((JObject)log[section]).Properties().Select(p => p.Name));
            var refTrial = (JObject)reference["History"][0];
            foreach (JObject t in log["History"])
            {
                CollectionAssert.AreEqual(refTrial.Properties().Select(p => p.Name), t.Properties().Select(p => p.Name));
                CollectionAssert.AreEqual(((JObject)refTrial["Spot"]).Properties().Select(p => p.Name),
                                          ((JObject)t["Spot"]).Properties().Select(p => p.Name));
            }

            var h = (JArray)log["History"];
            var sum = log["Summary"];
            Assert.AreEqual(36, h.Count);       // 34 + one retest each for the two missed training spots
            Assert.AreEqual(30, (int)sum["SpotsSeen"]);
            Assert.AreEqual(4, (int)sum["SpotsMissed"]);
            Assert.AreEqual(0, (int)sum["SpotsUntested"]);
            Assert.AreEqual(2, (int)sum["NumInitialMiss"]);
            Assert.AreEqual(2, (int)sum["NumIncorrectResponse"]);
            Assert.AreEqual(100f, (float)h.Last["PercentComplete"], 1e-3f);
            CollectionAssert.AreEqual(Enumerable.Range(0, 36), h.Select(t => (int)t["TrialIndex"]));

            foreach (var t in h)
            {
                int loc = (int)t["Spot"]["LocationID"];
                bool blind = Mathf.Abs(loc) == 3000;
                Assert.AreEqual(!blind && loc != 5005 && loc != -5002, (bool)t["Seen"], "loc " + loc);
                Assert.AreEqual(blind, (bool)t["TriggerPull"]);
                Assert.AreEqual(loc == 5005 ? 3 : blind || loc == -5002 ? -1 : 0, (int)t["ChoiceIndex"]);
                // timing identities of the reference log
                float el = (float)t["ElapsedTimeSec"], pres = (float)t["TimePresented"], fs = (float)t["FixationStart"];
                Assert.AreEqual(el, pres + (float)t["ResponseTimeSec"], 2e-4f);
                Assert.AreEqual((float)t["TotalTrialDurationSec"], el - fs, 2e-4f);
                Assert.Greater(pres - fs - (float)t["FixationDurationSec"], 0.05f);
                if (loc == -5002) Assert.GreaterOrEqual((float)t["ResponseTimeSec"], 2f);
                Assert.LessOrEqual((int)t["MeasuredFlashDurationMs"], 330);
            }
        }

        // One extra entry per History entry, matching it, with the head on the target at the flash.
        static void CheckExtra(TestSession session, JObject log)
        {
            var extra = JObject.Parse(File.ReadAllText(session.ExtraPath));
            Assert.AreEqual(Path.GetFileName(session.LogPath), (string)extra["Log"]);
            StringAssert.EndsWith("LogsExtra", Path.GetDirectoryName(session.ExtraPath));
            var h = (JArray)log["History"];
            var f = (JArray)extra["Flashes"];
            Assert.AreEqual(h.Count, f.Count);
            for (int i = 0; i < h.Count; i++)
            {
                Assert.AreEqual(i, (int)f[i]["HistoryIndex"]);
                Assert.AreEqual((int)h[i]["TrialIndex"], (int)f[i]["TrialIndex"]);
                Assert.AreEqual((int)h[i]["Spot"]["LocationID"], (int)f[i]["LocationID"]);
                Assert.AreEqual((float)h[i]["TimePresented"], (float)f[i]["TimePresented"], 1e-3f);
                Assert.Less((float)f[i]["HeadFromTargetDeg"], 3f, "head on the target at the flash");
                Assert.LessOrEqual((float)f[i]["TargetFromStraightAheadDeg"], 8.1f);
                if ((bool)h[i]["Responded"]) Assert.Greater((float)f[i]["ResponseHeadFromTargetDeg"], 3.9f);
            }
            Assert.GreaterOrEqual(((JArray)extra["StraightAhead"]).Count, 1);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator HeadsetOff_DiscardsTheTrialAndResumes()
        {
            var session = Make(out var head, out _, out var layout);
            var spots = layout.Select("Right", false).Take(3).ToList();
            var run = session.Test.Run(spots, 1);
            var co = head.StartCoroutine(run);
            while (!session.Test.AwaitingResponse) yield return null;
            head.Present = false;
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreEqual(0, session.Test.History.Count);
            head.Present = true;
            yield return co;
            Assert.AreEqual(3, session.Test.History.Count);
            Assert.AreEqual(1, session.Test.Extra.Discarded.Count);
            Assert.AreEqual("headset back", session.Test.Extra.Discarded[0].Reason);
            Assert.AreEqual("headset back", session.Test.Extra.StraightAhead.Last().Reason);
            Assert.IsTrue(session.Test.History.All(t => t.Seen));
        }
    }
}
