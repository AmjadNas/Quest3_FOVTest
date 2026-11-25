using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace FOVTest.Tests
{
    public class CoreTests
    {
        // The table in the reference log (DPVR P1 Pro).
        const string P1ProLut = "[0, 0.24] [0.05, 0.34] [0.1, 0.83] [0.15, 1.62] [0.2, 3.02] [0.25, 4.89] [0.3, 7.18] [0.35, 9.95] [0.4, 13.42] [0.45, 17.45] [0.5, 21.93] [0.55, 26.67] [0.6, 32.25] [0.65, 38.5] [0.7, 44.9] [0.75, 51.52] [0.8, 59.27] [0.85, 67.48] [0.9, 75.33] [0.95, 84.3] [1, 93.48]";

        /// <summary>History and Summary of a reference log. Not in the repository: put your own there to run these tests.</summary>
        static string FixtureText()
        {
            var path = Path.Combine(Application.dataPath, "FOVTest/Tests/Editor/Fixtures/vendor_history_summary.txt");
            if (!File.Exists(path)) Assert.Ignore("The reference fixture is not available.");
            return File.ReadAllText(path);
        }

        static List<TrialLog> ReferenceHistory()
        {
            var h = JObject.Parse(FixtureText())["History"].ToObject<List<TrialLog>>();
            for (int i = 0; i + 1 < h.Count; i++) h[i].Invalid = h[i].TrialIndex == h[i + 1].TrialIndex;
            return h;
        }

        [Test]
        public void Luminance_ReproducesReferenceBackgroundAndStimulus()
        {
            var lum = LuminanceModel.Parse(P1ProLut, "P1Pro", true);
            Assert.AreEqual(0.163590848f, lum.BrightnessFor(2f), 1e-4f);
            // Weber contrast 2 on 2 cd/m^2 = 6 cd/m^2, logged as RGBA(0.274, ...)
            Assert.AreEqual(0.274f, lum.BrightnessForContrast(2f, 2f), 5e-4f);
            Assert.AreEqual(P1ProLut, lum.ToLut());
        }

        [Test]
        public void Layout_Training30_MatchesReference()
        {
            var l = TestLayout.LoadBuiltin("Training-30deg");
            Assert.AreEqual(34, l.Spots.Count);
            Assert.AreEqual(17, l.Select("Left", true).Count);
            Assert.AreEqual(32, l.Select("Bilateral", false).Count);
            var refSummary = JObject.Parse(FixtureText())["Summary"];
            Assert.AreEqual((string)refSummary["LayoutData"], SummaryBuilder.Escape(l.Csv));
            Assert.AreEqual((string)refSummary["LayoutMetaJson"], l.MetaJson);
            Assert.AreEqual((string)refSummary["Layout"], l.FileName);
        }

        [Test]
        public void Layout_242_Has54PointsPerEyeMirrored()
        {
            var l = TestLayout.LoadBuiltin("24-2");
            foreach (var eye in new[] { "Left", "Right" })
                Assert.AreEqual(54, l.Spots.Count(s => s.Eye == eye && !s.IsBlindspot));
            Assert.AreEqual(-27f, l.Spots.Where(s => s.Eye == "Right").Min(s => s.X));   // nasal step
            Assert.AreEqual(27f, l.Spots.Where(s => s.Eye == "Left").Max(s => s.X));
            Assert.IsTrue(l.Spots.All(s => Mathf.Sqrt(s.X * s.X + s.Y * s.Y) < l.Meta.FixedCircleRadiusDegrees));
        }

        [Test]
        public void Summary_ReproducesReferenceLog()
        {
            var expected = (JObject)JObject.Parse(FixtureText())["Summary"];
            var layout = TestLayout.LoadBuiltin("Training-30deg");
            var s = SummaryBuilder.Build(ReferenceHistory(), 34, 101.126556f, 0f, 0.163590848f, 2f, layout,
                new DateTime(2025, 7, 2, 6, 19, 21), 1751462361);
            var actual = JObject.Parse(JsonConvert.SerializeObject(s));
            CollectionAssert.AreEqual(expected.Properties().Select(p => p.Name), actual.Properties().Select(p => p.Name));
            foreach (var p in expected.Properties())
            {
                if (p.Name == "LayoutDescription") continue;   // "Training-10deg" in the reference: a vendor label
                var a = actual[p.Name];
                if (p.Value.Type == JTokenType.Float)
                    Assert.AreEqual((double)p.Value, (double)a, 2e-6, p.Name);
                else if (p.Name == "TestLocations")
                    CompareLocations((JArray)p.Value, (JArray)a);
                else
                    Assert.IsTrue(JToken.DeepEquals(p.Value, a), $"{p.Name}: expected {p.Value}, got {a}");
            }
        }

        static void CompareLocations(JArray e, JArray a)
        {
            Assert.AreEqual(e.Count, a.Count);
            for (int i = 0; i < e.Count; i++)
                Assert.AreEqual(e[i].ToString(Formatting.None), a[i].ToString(Formatting.None));
        }

        [Test]
        public void HistoryLines_RoundTripByteForByte()
        {
            var lines = FixtureText().Split('\n').Where(l => l.StartsWith("{\"Spot\"")).Select(l => l.TrimEnd(',')).ToList();
            Assert.AreEqual(35, lines.Count);
            foreach (var line in lines)
                Assert.AreEqual(line, LogWriter.TrialLine(JsonConvert.DeserializeObject<TrialLog>(line)));
        }

        [Test]
        public void Tint_HasTheRequestedLuminance()
        {
            var lum = LuminanceModel.Quest3Nominal();
            foreach (var mix in new[] { new Color(1f, 0.35f, 0f), new Color(0.1f, 1f, 0.1f) })
            {
                var c = lum.Tint(18f, mix);
                float Lin(float v) => Mathf.Pow(v, 2.2f);
                float cd = 100f * (0.2126f * Lin(c.r) + 0.7152f * Lin(c.g) + 0.0722f * Lin(c.b));
                Assert.AreEqual(18f, cd, 1.5f);
            }
            var orange = lum.Tint(18f, new Color(1f, 0.35f, 0f));
            Assert.Greater(orange.r, orange.g + 0.1f);
            Assert.Greater(orange.g, orange.b + 0.1f);
        }

        [Test]
        public void OldSettingsFile_GetsTheNewDotSizes_AndKeepsTheRest()
        {
            var s = AppSettings.Parse("{\"Patient\":{\"UserID\":77},\"Test\":{\"FixationDotDiameterDeg\":0.6,\"PointerDiameterDeg\":0.5,\"ResponseThresholdDeg\":5.5}}");
            Assert.AreEqual(0.9f, s.Test.FixationDotDiameterDeg);
            Assert.AreEqual(0.75f, s.Test.PointerDiameterDeg);
            Assert.AreEqual(5.5f, s.Test.ResponseThresholdDeg);
            Assert.AreEqual(77, s.Patient.UserID);
            // sizes the experimenter sets from now on are kept
            s.Test.FixationDotDiameterDeg = 1.2f;
            Assert.AreEqual(1.2f, AppSettings.Parse(JsonConvert.SerializeObject(s)).Test.FixationDotDiameterDeg);
        }

        [Test]
        public void StandardSize_LeavesTheReferenceLayoutUntouched()
        {
            var l = TestLayout.LoadBuiltin("Training-30deg");
            l.SetDiameter(new TestParameters().StimulusDiameterDeg);
            Assert.AreEqual((string)JObject.Parse(FixtureText())["Summary"]["LayoutData"], SummaryBuilder.Escape(l.Csv));
            Assert.AreEqual("VVPC_Training-30deg.csv", l.FileName);
            Assert.AreEqual("Training-30deg", l.Name);
        }

        [TestCase("Training-30deg")]
        [TestCase("24-2")]
        public void LargeSize_IsInTheSpotsTheCsvAndTheName(string name)
        {
            var original = TestLayout.LoadBuiltin(name);
            var l = TestLayout.LoadBuiltin(name);
            l.SetDiameter(1.72f);
            StringAssert.EndsWith("_1.72deg.csv", l.FileName);
            StringAssert.Contains("1.72deg", l.Name);
            var parsed = TestLayout.Parse(l.Name, l.FileName, l.Csv, l.MetaJson);
            Assert.AreEqual(original.Spots.Count, parsed.Spots.Count);
            for (int i = 0; i < parsed.Spots.Count; i++)
            {
                Assert.AreEqual(1.72f, l.Spots[i].DiameterDeg);
                Assert.AreEqual(1.72f, parsed.Spots[i].DiameterDeg);
                // nothing else changes
                Assert.AreEqual(original.Spots[i].X, parsed.Spots[i].X);
                Assert.AreEqual(original.Spots[i].Y, parsed.Spots[i].Y);
                Assert.AreEqual(original.Spots[i].Contrast, parsed.Spots[i].Contrast);
                Assert.AreEqual(original.Spots[i].LocationId, parsed.Spots[i].LocationId);
            }
        }

        [TestCase("Training-30deg")]
        [TestCase("24-2")]
        public void LargeSize_ClearsTheDividersAndTheLockRing(string name)
        {
            foreach (var s in TestLayout.LoadBuiltin(name).Spots)
            {
                float ecc = Mathf.Sqrt(s.X * s.X + s.Y * s.Y), radius = 1.72f / 2f + 0.05f;
                float toDivider = ecc * Mathf.Sin(30f * Mathf.Deg2Rad) - 0.125f - radius;
                Assert.Greater(toDivider, 0.5f, "divider, location " + s.LocationId);
                Assert.Greater(ecc - radius - 1.3f, 0.5f, "lock ring, location " + s.LocationId);
            }
        }

        [Test]
        public void NoCompletedTrial_StillGivesAValidLog()
        {
            var s = SummaryBuilder.Build(new List<TrialLog>(), 34, 300f, 12f, 0.16f, 2f,
                TestLayout.LoadBuiltin("Training-30deg"), new DateTime(2026, 11, 25, 12, 0, 0), 1795600000);
            Assert.AreEqual(34, s.SpotsUntested);
            Assert.AreEqual(0, s.NumStimuliPresented);
            Assert.AreEqual(0f, s.PercentCorrect);
            Assert.AreEqual(0f, s.GuessRate);
            Assert.AreEqual(0f, s.MeanTrialDurationSec);
            Assert.AreEqual(5, s.TestMinutes);
            var header = new LogHeader { PatientSettings = new PatientSettings(), ActivitySettings = new ActivitySettings() };
            var log = JObject.Parse(LogWriter.ToJson(header, new List<TrialLog>(), s, 1795600000));
            Assert.AreEqual(0, ((JArray)log["History"]).Count);
            // the same summary fields as a complete log
            CollectionAssert.AreEqual(((JObject)JObject.Parse(FixtureText())["Summary"]).Properties().Select(p => p.Name),
                                      ((JObject)log["Summary"]).Properties().Select(p => p.Name));
        }

        [Test]
        public void HeadAngles_HaveTheDocumentedSigns()
        {
            Assert.AreEqual(30f, HeadAngles.Yaw(Quaternion.Euler(0, 30f, 0) * Vector3.forward), 1e-3f);      // right
            Assert.AreEqual(20f, HeadAngles.Pitch(Quaternion.Euler(-20f, 0, 0) * Vector3.forward), 1e-3f);   // up
            var rightEarDown = Quaternion.Euler(0, 0, -15f);
            Assert.Less((rightEarDown * Vector3.right).y, 0f);
            Assert.AreEqual(15f, HeadAngles.Roll(rightEarDown), 1e-3f);
            Assert.AreEqual(15f, HeadAngles.Roll(Quaternion.Euler(-20f, 40f, 0) * rightEarDown), 1e-2f);
        }

        [Test]
        public void SpotColor_FormatsLikeReference() =>
            Assert.AreEqual("RGBA(0.274, 0.274, 0.274, 1.000)", new Color(0.2743f, 0.2743f, 0.2743f, 1).ToString());

        [Test]
        public void Scheduler_RunsEveryLocationOnceWhenAllSeen()
        {
            var sch = new StimulusScheduler(TestLayout.LoadBuiltin("Training-30deg").Spots, 1);
            var ids = new HashSet<int>();
            while (sch.HasNext) { ids.Add(sch.Next().Id); sch.Report(Outcome.Seen); }
            Assert.AreEqual(34, ids.Count);
            Assert.AreEqual(34, sch.CountedTrials);
            Assert.AreEqual(34, sch.LocationsDone);
        }

        [Test]
        public void Scheduler_RetestsMissesAndRepeatsInvalid()
        {
            var l = TestLayout.LoadBuiltin("Training-30deg");
            var sch = new StimulusScheduler(l.Spots, 2);
            int invalid = 0;
            var counts = new Dictionary<int, int>();
            while (sch.HasNext)
            {
                var s = sch.Next();
                if (invalid == 0) { invalid++; sch.Report(Outcome.Invalid); Assert.AreEqual(0, sch.CountedTrials); continue; }
                counts[s.Id] = counts.TryGetValue(s.Id, out var n) ? n + 1 : 1;
                sch.Report(Outcome.NoResponse);
            }
            foreach (var s in l.Spots)
                Assert.AreEqual(s.IsBlindspot ? 1 : 2, counts[s.Id], s.Rule);   // training retests once, blind spot never
            Assert.AreEqual(66, sch.CountedTrials);
        }

        [Test]
        public void Scheduler_24_2_RetestsTwiceThenStops()
        {
            var sch = new StimulusScheduler(TestLayout.LoadBuiltin("24-2").Spots, 3);
            int n = 0;
            while (sch.HasNext) { sch.Report(Outcome.Incorrect); n++; }
            Assert.AreEqual(108 * 3 + 2, n);
        }

        [Test]
        public void Geometry_RoundTripsAtObliqueFixation()
        {
            var fix = Quaternion.Euler(-7f, 11f, 0) * Vector3.forward;
            var up = Quaternion.Euler(-7f, 11f, 15f) * Vector3.up;     // head rolled
            foreach (var s in TestLayout.LoadBuiltin("24-2").Spots)
            {
                var d = VisualAngle.Direction(fix, up, s.X, s.Y);
                VisualAngle.Measure(fix, up, d, out var ecc, out var polar);
                Assert.AreEqual(Mathf.Sqrt(s.X * s.X + s.Y * s.Y), ecc, 0.02f);
                Assert.AreEqual(0f, Mathf.DeltaAngle(s.PolarDeg, polar), 0.05f);
            }
        }

        [Test]
        public void Geometry_RightIsRightAndUpIsUp()
        {
            var d = VisualAngle.Direction(Vector3.forward, Vector3.up, 15f, 0f);
            Assert.Greater(d.x, 0.2f);
            Assert.AreEqual(0f, d.y, 1e-5f);
            Assert.Greater(VisualAngle.Direction(Vector3.forward, Vector3.up, 0f, 15f).y, 0.2f);
        }

        [TestCase(45f, 45f, 0)]
        [TestCase(45f, 74f, 0)]
        [TestCase(45f, 16f, 0)]
        [TestCase(45f, 76f, 1)]
        [TestCase(45f, 14f, 5)]
        [TestCase(45f, -135f, 3)]
        [TestCase(-170f, 170f, 0)]
        public void ChoiceSector_Accepts30DegEitherSide(float stim, float resp, int expected) =>
            Assert.AreEqual(expected, VisualAngle.ChoiceSector(stim, resp));
    }
}
