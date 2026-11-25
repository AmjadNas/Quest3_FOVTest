using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace FOVTest.Tests
{
    /// <summary>The two phases of a trial, the areas, the answer mark and the progress bar.</summary>
    public class PhaseTests
    {
        GameObject go;
        SimulatedParticipant head;
        Stage stage;
        TestLayout layout;
        TestSession session;

        [SetUp]
        public void Make()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 72;
            go = new GameObject("sim");
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.fieldOfView = 80f;
            cam.nearClipPlane = 0.05f;
            head = go.AddComponent<SimulatedParticipant>();
            var settings = new AppSettings();
            settings.Test.MaxDroppedFrames = 1000;
            settings.Test.InterTrialSec = 0.3f;
            var lum = LuminanceModel.Quest3Nominal();
            stage = new Stage(cam, lum, settings.Test, Shader.Find("FOVTest/Spot"));
            layout = TestLayout.LoadBuiltin("Training-30deg");
            stage.Configure(layout.Meta, 2f);
            session = new TestSession(settings, layout, stage, head, lum);
            head.Stage = stage;
            head.Test = session.Test;
            session.Test.SetPrimaryToHead();
        }

        [TearDown]
        public void Clean()
        {
            Object.DestroyImmediate(go);
            for (var s = GameObject.Find("Stage"); s != null; s = GameObject.Find("Stage")) Object.DestroyImmediate(s);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Phases_Feedback_AndProgress()
        {
            head.Strategy = s => s.LocationId == 5001 ? "wrong" : s.LocationId == 5002 ? "trigger" : "seen";
            // no retests, so that each location gives exactly one answer
            var spots = layout.Spots.Where(s => s.LocationId >= 5000 && s.LocationId <= 5003).ToList();
            foreach (var s in spots) s.TimesToRetestIfMissed = 0;
            var progress = new List<float>();
            var marks = new List<Outcome>();
            int locks = 0;
            session.Test.TargetLocked = () =>
            {
                locks++;
                progress.Add(stage.Progress);
                Assert.IsTrue(stage.ResponsePhaseShown);
            };
            session.Test.Answered = o =>
            {
                marks.Add(o);
                Assert.AreEqual(o, stage.FeedbackShown);
                Assert.IsFalse(stage.ResponsePhaseShown);
            };
            var co = head.StartCoroutine(session.Test.Run(spots, 5));
            bool sawPhase1 = false, sawPhase2 = false;
            while (session.Test.History.Count < 4)
            {
                if (session.Test.AwaitingResponse)
                {
                    sawPhase2 = true;
                    Assert.IsTrue(stage.ResponsePhaseShown, "areas stay on while responding");
                    Assert.IsFalse(stage.CircleShown, "the circle is hidden while responding");
                    Assert.IsNull(stage.FeedbackShown);
                    CheckAreas(session.Test.CurrentSpot);
                }
                else if (!stage.ResponsePhaseShown && stage.FeedbackShown == null) sawPhase1 = true;
                yield return null;
            }
            yield return co;
            Assert.IsTrue(sawPhase1 && sawPhase2);
            Assert.GreaterOrEqual(locks, 4);
            CollectionAssert.AreEqual(new[] { 0f, 0.25f, 0.5f, 0.75f }, progress.Distinct().ToArray());
            Assert.AreEqual(2, marks.Count(m => m == Outcome.Seen));
            Assert.AreEqual(1, marks.Count(m => m == Outcome.Incorrect));
            Assert.AreEqual(1, marks.Count(m => m == Outcome.NoResponse));
        }

        // The stimulus lies 30 degrees from the two nearest dividers: the middle of one of six areas.
        void CheckAreas(SpotDef spot)
        {
            var angles = new List<float>();
            for (int i = 0; i < Stage.Sectors; i++)
            {
                VisualAngle.Measure(stage.FixationDir, head.HeadUp, stage.Divider(i).localPosition, out _, out var polar);
                angles.Add(Mathf.Abs(Mathf.DeltaAngle(polar, spot.PolarDeg)));
            }
            angles.Sort();
            // the head turns while responding, which turns 'up' slightly; the areas themselves stay put
            Assert.AreEqual(30f, angles[0], 4f);
            Assert.AreEqual(30f, angles[1], 4f);
            Assert.AreEqual(90f, angles[2], 4f);
        }

        [UnityTest]
        public IEnumerator Screens_AreDrawn()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("needs a GPU");
            var spot = layout.Spots.First(s => s.Eye == "Left" && s.LocationId == -5008);   // a mono camera draws the left eye
            var up = Vector3.up;
            yield return null;

            stage.ShowFixation(Vector3.forward, Vector3.forward, EyeMask.Left, false, 0.4f);
            stage.UpdatePointer(VisualAngle.Direction(Vector3.forward, up, 5, 3), up, true);
            var phase1 = Shot("1_point_at_target");

            stage.UpdatePointer(Vector3.forward, up, true);
            stage.ShowResponsePhase(spot.PolarDeg, up);
            var locked = Shot("2_locked");
            stage.ShowStimulus(spot, Vector3.zero, up);
            var flash = Shot("3_stimulus");
            Assert.Greater(Difference(phase1, locked), 500, "locking must change the picture clearly");
            Assert.Greater(Difference(locked, flash), 10, "stimulus (0.43 deg is about 20 pixels here)");

            var dir = VisualAngle.Direction(Vector3.forward, up, spot.X * 0.4f, spot.Y * 0.4f);
            stage.UpdatePointer(dir, up, false);
            var shots = new List<Color32[]>();
            foreach (var o in new[] { Outcome.Seen, Outcome.Incorrect, Outcome.NoResponse })
            {
                stage.HideTarget();
                stage.ShowFeedback(o, dir);
                shots.Add(Shot("4_feedback_" + o));
            }
            Assert.Greater(shots[0].Count(c => c.g > c.r + 20), 50, "green tick");
            Assert.Greater(shots[1].Count(c => c.r > c.g + 20), 50, "red cross");
            Assert.Greater(Difference(shots[0], shots[2]), 100);

            // right-eye items must not appear in the left eye's picture
            stage.ShowFixation(Vector3.forward, Vector3.forward, EyeMask.Right, false, 0.4f);
            stage.ShowResponsePhase(spot.PolarDeg, up);
            var other = Shot("5_right_eye_trial_seen_by_left_eye");
            Assert.AreEqual(0, CentreBright(other), "nothing of a right-eye trial may reach the left eye");

            stage.Configure(TestLayout.LoadBuiltin("24-2").Meta, 25f);
            stage.ShowFixation(Vector3.forward, Vector3.forward, EyeMask.Left, false, 0.7f);
            Shot("6_242_point_at_target");
            stage.ShowResponsePhase(30f, up);
            Shot("7_242_locked");
            stage.HideTarget();
            stage.ShowFeedback(Outcome.Seen, dir);
            Shot("8_242_feedback");
        }

        [UnityTest]
        public IEnumerator LargeFlash_HasSixteenTimesTheArea_AndTheSameBrightness()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("needs a GPU");
            yield return null;
            var spot = layout.Spots.First(s => s.LocationId == -5008);
            int Area(float deg, string shot, out int peak)
            {
                spot.DiameterDeg = deg;
                stage.ShowFixation(Vector3.forward, Vector3.forward, EyeMask.Left, false, 0f);
                stage.ShowResponsePhase(spot.PolarDeg, Vector3.up);
                var before = Shot(shot + "_before");
                stage.ShowStimulus(spot, Vector3.zero, Vector3.up);
                var after = Shot(shot);
                stage.HideStimulus();
                peak = after.Where((c, i) => Mathf.Abs(c.g - before[i].g) > 8).Max(c => (int)c.g);
                // half-height area, as the diameter is defined
                int half = (int)((stage.BackgroundBrightness * 255f + peak) / 2f);
                return after.Where((c, i) => Mathf.Abs(c.g - before[i].g) > 8 && c.g >= half).Count();
            }
            int small = Area(0.43f, "9_size_0.43", out var p1), large = Area(1.72f, "9_size_1.72", out var p2);
            Assert.AreEqual(16f, large / (float)small, 3.5f);
            Assert.AreEqual(p1, p2, 2);
        }

        Color32[] Shot(string name)
        {
            var cam = go.GetComponent<Camera>();
            var rt = new RenderTexture(1000, 1000, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1000, 1000, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1000, 1000), 0, 0);
            tex.Apply();
            cam.targetTexture = null;
            RenderTexture.active = null;
            var dir = Path.Combine(Application.temporaryCachePath, "Screens");
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
            return tex.GetPixels32();
        }

        static int Difference(Color32[] a, Color32[] b) =>
            a.Zip(b, (x, y) => Mathf.Abs(x.r - y.r) > 8 || Mathf.Abs(x.g - y.g) > 8 ? 1 : 0).Sum();

        // Pixels brighter than the background within about 15 degrees of the centre.
        int CentreBright(Color32[] px)
        {
            int bg = Mathf.RoundToInt(stage.BackgroundBrightness * 255f), n = 0;
            for (int y = 340; y < 660; y++)
                for (int x = 340; x < 660; x++)
                    if (px[y * 1000 + x].g > bg + 6) n++;
            return n;
        }
    }
}
