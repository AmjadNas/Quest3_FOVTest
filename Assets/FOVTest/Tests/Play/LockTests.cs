using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FOVTest.Tests
{
    /// <summary>A head the test moves directly.</summary>
    public class ManualHead : HeadInput
    {
        public Quaternion Rotation = Quaternion.identity;
        public bool Trigger, Abort;
        public int Recenters;
        public override int RecenterCount => Recenters;
        public override Vector3 HeadPosition => Vector3.zero;
        public override Quaternion HeadRotation => Rotation;
        public override Vector3 EyePosition(EyeMask e) => Vector3.zero;
        public override bool Active => true;
        public override bool TriggerDown => Trigger;
        public override bool ConfirmDown => false;
        public override bool AbortHeld => Abort;
        public override Vector2Int StickStep => Vector2Int.zero;
        public void Face(Vector3 dir, float roll = 0f) =>
            Rotation = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(0, 0, roll);
    }

    public class LockTests
    {
        GameObject go;
        ManualHead head;
        Stage stage;
        PerimetryTest test;
        TestLayout layout;
        int locks;

        [SetUp]
        public void Make()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 72;
            go = new GameObject("sim");
            var cam = go.AddComponent<Camera>();
            head = go.AddComponent<ManualHead>();
            var settings = new AppSettings();
            settings.Test.MaxDroppedFrames = 1000;
            settings.Test.InterTrialSec = 0.2f;
            var lum = LuminanceModel.Quest3Nominal();
            stage = new Stage(cam, lum, settings.Test, Shader.Find("FOVTest/Spot"));
            layout = TestLayout.LoadBuiltin("Training-30deg");
            stage.Configure(layout.Meta, 2f);
            test = new PerimetryTest(stage, head, settings, layout);
            test.SetPrimaryToHead();
            locks = 0;
            test.TargetLocked = () => locks++;
        }

        [TearDown]
        public void Clean()
        {
            Object.DestroyImmediate(go);
            for (var s = GameObject.Find("Stage"); s != null; s = GameObject.Find("Stage")) Object.DestroyImmediate(s);
        }

        List<SpotDef> OneSpot()
        {
            var s = layout.Spots.First(x => x.LocationId == 5005);
            s.TimesToRetestIfMissed = 0;
            return new List<SpotDef> { s };
        }

        IEnumerator Until(System.Func<bool> done, float timeout = 5f)
        {
            float t = Time.realtimeSinceStartup;
            while (!done() && Time.realtimeSinceStartup - t < timeout) yield return null;
            Assert.IsTrue(done(), "timed out");
        }

        [UnityTest]
        public IEnumerator OnceLocked_ItStaysLocked_WhenThePointerSlipsOff()
        {
            var co = go.GetComponent<ManualHead>().StartCoroutine(test.Run(OneSpot()));
            yield return null;
            var target = stage.FixationDir;
            head.Face(target);
            yield return Until(() => locks == 1);
            head.Face(VisualAngle.Direction(target, Vector3.up, 2f, 0f));       // off the dot, not yet an answer
            for (int i = 0; i < 60; i++)
            {
                Assert.IsTrue(stage.ResponsePhaseShown, "must not go back to phase 1");
                Assert.IsFalse(stage.CircleShown, "the circle disappears in phase 2");
                yield return null;
            }
            Assert.IsTrue(test.AwaitingResponse, "the stimulus follows the lock");
            Assert.AreEqual(1, locks);
            head.Trigger = true;
            yield return co;
            Assert.AreEqual(1, test.History.Count);
            Assert.IsTrue(test.History[0].TriggerPull);
        }

        [UnityTest]
        public IEnumerator SweepingAcrossTheTarget_DoesNotLock()
        {
            head.StartCoroutine(test.Run(OneSpot()));
            yield return null;
            var target = stage.FixationDir;
            float t0 = Time.unscaledTime;
            while (Time.unscaledTime - t0 < 2f)
            {
                // passes over the target at 90 to 120 degrees per second
                float x = Mathf.PingPong((Time.unscaledTime - t0) * 120f, 30f) - 15f;
                head.Face(VisualAngle.Direction(target, Vector3.up, x, 0f));
                yield return null;
            }
            Assert.AreEqual(0, locks);
            head.Face(target);      // and stopping on it does
            yield return Until(() => locks == 1);
        }

        [UnityTest]
        public IEnumerator ResponseTooSoonToBeCausedByTheStimulus_IsRepeated()
        {
            var co = head.StartCoroutine(test.Run(OneSpot()));
            yield return null;
            head.Face(stage.FixationDir);
            yield return Until(() => test.AwaitingResponse);
            head.Face(VisualAngle.Direction(stage.FixationDir, Vector3.up, 10f, 0f));
            yield return Until(() => test.History.Count == 1);
            Assert.IsTrue(test.History[0].Invalid);
            Assert.Less(test.History[0].ResponseTimeSec, 0.15f);
            head.Face(stage.FixationDir);
            yield return Until(() => locks == 1 || locks == 2);
            yield return Until(() => { head.Face(stage.FixationDir); return locks == 2 && test.AwaitingResponse; });
            head.Trigger = true;
            yield return co;
            Assert.AreEqual(2, test.History.Count);
            Assert.AreEqual(0, test.History[0].TrialIndex);
            Assert.AreEqual(0, test.History[1].TrialIndex, "the repeat takes the same index, as in the reference log");
            Assert.IsFalse(test.History[1].Invalid);
        }

        [UnityTest]
        public IEnumerator BarAndMark_StayLevel_WhenTheHeadIsTilted()
        {
            float bar = 99f, mark = 99f;
            test.TargetLocked = () => { locks++; };
            test.Answered = o => mark = Mathf.Abs(stage.AnswerMark.right.y);
            var primary = head.HeadForward;
            head.Face(primary, 25f);        // head tilted to the side
            var co = head.StartCoroutine(test.Run(OneSpot()));
            yield return null;
            bar = Mathf.Abs(stage.ProgressBar.right.y);
            Assert.Less(bar, 1e-3f, "progress bar");
            Assert.Less(Mathf.Abs(stage.ProgressBar.localPosition.x), 1e-3f, "straight below the centre");
            head.Face(stage.FixationDir, 25f);
            yield return Until(() => test.AwaitingResponse);
            head.Trigger = true;        // no answer: a single bar
            yield return co;
            Assert.Less(mark, 1e-3f, "answer mark");
        }

        TestSession Session()
        {
            var settings = new AppSettings();
            settings.Patient.UserID = 54321;
            var s = new TestSession(settings, layout, stage, head, LuminanceModel.Quest3Nominal(),
                System.IO.Path.Combine(Application.temporaryCachePath, "Unfinished"));
            s.Test.Plan(34);
            s.Test.SetPrimaryToHead();
            return s;
        }

        [UnityTest]
        public IEnumerator NeverFindingTheTarget_EndsAtTheTimeLimit_AndSavesALog()
        {
            layout.Meta.TimeLimitMinutes = 1f / 60f;
            var s = Session();
            head.Face(Quaternion.Euler(0, 60f, 0) * Vector3.forward);       // looking elsewhere all the time
            float t0 = Time.realtimeSinceStartup;
            yield return s.Test.Run(layout.Select("Bilateral", true));
            Assert.Less(Time.realtimeSinceStartup - t0, 3f);
            Assert.IsTrue(s.Test.TimedOut);
            Assert.AreEqual(0, s.Test.History.Count);
            Assert.IsNull(s.Save());
            var log = Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText(s.LogPath));
            Assert.AreEqual(34, (int)log["Summary"]["SpotsUntested"]);
            Assert.AreEqual(1f, (float)log["Summary"]["TestDurationSec"], 0.3f);
            System.IO.File.Delete(s.LogPath);
        }

        [UnityTest]
        public IEnumerator TheTimeLimit_DoesNotCutATrialThatIsUnderWay()
        {
            layout.Meta.TimeLimitMinutes = 1f / 60f;
            var s = Session();
            var co = head.StartCoroutine(s.Test.Run(layout.Select("Bilateral", true)));
            yield return null;
            head.Face(stage.FixationDir);
            yield return Until(() => s.Test.AwaitingResponse);
            yield return co;        // times out 2 s after the flash, well after the 1 s limit
            Assert.AreEqual(1, s.Test.History.Count);
            Assert.IsTrue(s.Test.TimedOut);
        }

        // Recentering turns the tracking space: the head keeps still in the room but its direction jumps.
        [UnityTest]
        public IEnumerator RecenteringDuringATrial_DiscardsIt_AndMovesTheTargetsToTheNewStraightAhead()
        {
            var s = Session();
            var co = head.StartCoroutine(s.Test.Run(OneSpot()));
            yield return null;
            head.Face(stage.FixationDir);
            yield return Until(() => s.Test.AwaitingResponse);
            var newHeading = Quaternion.Euler(0, 40f, 0) * Vector3.forward;
            head.Recenters++;
            head.Face(newHeading);
            for (int i = 0; i < 5; i++) yield return null;
            Assert.AreEqual(0, s.Test.History.Count, "the jump must not count as an answer");
            Assert.AreEqual("recentered", s.Test.Extra.Discarded.Single().Reason);
            Assert.AreEqual(40f, s.Test.Extra.StraightAhead.Last().YawDeg, 0.1f);
            Assert.Less(Vector3.Angle(stage.FixationDir, newHeading), 8.1f, "targets follow the new straight ahead");
            head.Face(stage.FixationDir);
            yield return Until(() => s.Test.AwaitingResponse);
            head.Trigger = true;
            yield return co;
            Assert.AreEqual(1, s.Test.History.Count, "the location is tested again");
        }

        [UnityTest]
        public IEnumerator AHeadJumpWithoutARecenterEvent_IsTreatedTheSame()
        {
            var s = Session();
            head.StartCoroutine(s.Test.Run(OneSpot()));
            yield return null;
            head.Face(stage.FixationDir);
            yield return Until(() => s.Test.AwaitingResponse);
            head.Face(Quaternion.Euler(0, 40f, 0) * Vector3.forward);       // the event may come a frame later, or never
            for (int i = 0; i < 5; i++) yield return null;
            Assert.AreEqual(0, s.Test.History.Count);
            Assert.AreEqual("recentered", s.Test.Extra.Discarded.Single().Reason);
        }

        [UnityTest]
        public IEnumerator RecenteringBetweenTrials_LosesNoTrial()
        {
            var s = Session();
            var spots = layout.Spots.Where(x => x.LocationId == 5005 || x.LocationId == 5006).ToList();
            foreach (var x in spots) x.TimesToRetestIfMissed = 0;
            var co = head.StartCoroutine(s.Test.Run(spots));
            yield return null;
            head.Face(stage.FixationDir);
            yield return Until(() => s.Test.AwaitingResponse);
            head.Trigger = true;
            yield return Until(() => s.Test.History.Count == 1);
            head.Trigger = false;
            head.Recenters++;       // during the pause between trials
            head.Face(Quaternion.Euler(0, 30f, 0) * Vector3.forward);
            yield return Until(() => s.Test.Extra.StraightAhead.Last().Reason == "recentered");
            yield return null;
            Assert.AreEqual(0, s.Test.Extra.Discarded.Count);
            Assert.Less(Vector3.Angle(stage.FixationDir, head.HeadForward), 8.1f);
            head.Face(stage.FixationDir);
            yield return Until(() => s.Test.AwaitingResponse);
            head.Trigger = true;
            yield return co;
            Assert.AreEqual(2, s.Test.History.Count);
        }

        [UnityTest]
        public IEnumerator StoppingDuringPractice_SavesALog_WithEverythingUntested()
        {
            var s = Session();
            StringAssert.Contains("not started", s.Save());
            head.Face(Quaternion.Euler(0, 60f, 0) * Vector3.forward);
            var co = head.StartCoroutine(s.Test.Practice(layout.Select("Bilateral", false), 4));
            yield return null;
            head.Abort = true;
            yield return co;
            Assert.IsTrue(s.Test.Aborted);
            Assert.IsNull(s.Save());
            var log = Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText(s.LogPath));
            Assert.AreEqual(34, (int)log["Summary"]["SpotsUntested"]);
            Assert.AreEqual(0, ((Newtonsoft.Json.Linq.JArray)log["History"]).Count);
            System.IO.File.Delete(s.LogPath);
        }

        [UnityTest]
        public IEnumerator Sounds_ReachTheOutput()
        {
            go.AddComponent<AudioListener>();
            var sounds = new Sounds(go) { Volume = 50f };
            foreach (var play in new System.Action[] { sounds.Locked, () => sounds.Answer(Outcome.Seen),
                         () => sounds.Answer(Outcome.Incorrect), () => sounds.Answer(Outcome.NoResponse) })
            {
                var buf = new float[1024];
                float peak = 0f, t0 = Time.realtimeSinceStartup;
                play();
                while (Time.realtimeSinceStartup - t0 < 0.6f)
                {
                    AudioListener.GetOutputData(buf, 0);
                    peak = Mathf.Max(peak, buf.Max(v => Mathf.Abs(v)));
                    yield return null;
                }
                Debug.Log($"[FOVTest] sound peak {peak:0.00}");
                Assert.Greater(peak, 0.1f);
            }
        }
    }
}
