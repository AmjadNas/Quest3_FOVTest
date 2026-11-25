using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.XR;

namespace FOVTest
{
    /// <summary>Menu, instructions, practice, test, results. Everything is driven by a thumbstick, A/X, B/Y and the trigger.</summary>
    public class AppController : MonoBehaviour
    {
        const float MenuBackground = 0.12f;     // dark, so the eyes are already close to the test background
        const float MenuText = 0.55f;
        const int PracticeTrials = 4;

        [SerializeField] Shader spotShader;
        [SerializeField] Shader textShader;

        AppSettings settings;
        LuminanceModel lum;
        HeadInput input;
        Stage stage;
        TextMesh text;
        Material textMaterial;
        Camera cam;
        TestSession session;
        Sounds sounds;
        readonly List<Spot> map = new List<Spot>();
        Transform mapRoot;

        public void SetShaders(Shader spot, Shader txt)
        {
            spotShader = spot;
            textShader = txt;
        }

        /// <summary>Run without a headset (automated tests).</summary>
        public void UseInput(HeadInput replacement) => input = replacement;

        IEnumerator Start()
        {
            settings = AppSettings.Load();
            lum = AppSettings.LoadLuminance();
            bool headset = input == null;
            if (headset) input = gameObject.AddComponent<QuestInput>();
            yield return null;
            cam = Camera.main;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.allowHDR = false;
            cam.nearClipPlane = 0.05f;
            if (headset) ConfigureHeadset();
            CreateText();
            sounds = new Sounds(gameObject);
            stage = new Stage(cam, lum, settings.Test, spotShader);
            if (SelfTest.Requested)
            {
                yield return SelfTest.Run(gameObject, stage, input, settings, lum, s => text.text = s);
                yield break;
            }
            while (true)
            {
                yield return Menu();
                yield return RunTest();
            }
        }

        void ConfigureHeadset()
        {
            XRSettings.eyeTextureResolutionScale = settings.Test.RenderScale;
            QualitySettings.antiAliasing = 4;
            // Foveation would blur exactly the part of the field being tested.
            OVRManager.foveatedRenderingLevel = OVRManager.FoveatedRenderingLevel.Off;
            OVRManager.useDynamicFoveatedRendering = false;
            OVRManager.suggestedCpuPerfLevel = OVRManager.ProcessorPerformanceLevel.SustainedHigh;
            OVRManager.suggestedGpuPerfLevel = OVRManager.ProcessorPerformanceLevel.SustainedHigh;
            if (OVRManager.display != null) OVRManager.display.displayFrequency = settings.Test.DisplayFrequencyHz;
            // Native gamut: no colour conversion between the grey levels we write and the panel.
            if (OVRManager.instance != null) OVRManager.instance.colorGamut = OVRManager.instance.nativeColorGamut;
        }

        void LateUpdate()
        {
            if (stage != null) stage.Follow(input.HeadPosition);
        }

        void OnApplicationPause(bool paused)
        {
            if (paused && session != null) session.Save();
        }

        void OnApplicationQuit()
        {
            if (session != null) session.Save();
        }

        // ---------------------------------------------------------------- menu

        class Item
        {
            public string Label;
            public Func<string> Value;
            public Action<int> Change;
            public Action Confirm;
        }

        IEnumerator Menu()
        {
            stage.HideAll();
            stage.SetBackground(MenuBackground);
            PlaceText();
            bool start = false, calibrate = false;
            var a = settings.Activity;
            var p = settings.Patient;
            var t = settings.Test;
            var layouts = TestLayout.Builtin.Select(b => b.name).ToList();
            int digit = -1;     // participant ID digit being edited, -1 = not editing
            var items = new List<Item>
            {
                new Item { Label = "Participant ID", Value = () => IdText(p.UserID, digit), Confirm = () => digit = digit < 0 ? 0 : -1 },
                new Item { Label = "Age", Value = () => p.Age.ToString(), Change = d => p.Age = Mathf.Clamp(p.Age + d, 0, 120) },
                new Item { Label = "Dominant eye", Value = () => p.DominantEye, Change = _ => p.DominantEye = p.DominantEye == "Right" ? "Left" : "Right" },
                new Item { Label = "Layout", Value = () => a.Layout, Change = d => a.Layout = Step(layouts, a.Layout, d) },
                new Item { Label = "Flash size", Value = () => SizeText(t.StimulusDiameterDeg), Change = _ => t.StimulusDiameterDeg = t.StimulusDiameterDeg < 1f ? SizeV : SizeIII },
                new Item { Label = "Eyes", Value = () => a.TestEyes, Change = d => a.TestEyes = Step(new List<string> { "Bilateral", "Left", "Right" }, a.TestEyes, d) },
                new Item { Label = "Blind spot checks", Value = () => YesNo(a.IncludeBlindspot), Change = _ => a.IncludeBlindspot = !a.IncludeBlindspot },
                new Item { Label = "Instructions and practice", Value = () => YesNo(a.TutorialEnabled), Change = _ => a.TutorialEnabled = !a.TutorialEnabled },
                new Item { Label = "Show pointer while responding", Value = () => YesNo(t.ShowPointerWhileResponding), Change = _ => t.ShowPointerWhileResponding = !t.ShowPointerWhileResponding },
                new Item { Label = "Show areas", Value = () => YesNo(t.ShowDividers), Change = _ => t.ShowDividers = !t.ShowDividers },
                new Item { Label = "Right/wrong feedback", Value = () => YesNo(t.Feedback), Change = _ => t.Feedback = !t.Feedback },
                new Item { Label = "Sound", Value = () => p.SoundFXVolume.ToString("0") + " %", Change = d => p.SoundFXVolume = Mathf.Clamp(p.SoundFXVolume + d * 10f, 0f, 100f) },
                new Item { Label = "Luminance patch (for calibration)", Value = () => "", Confirm = () => calibrate = true },
                new Item { Label = "START TEST", Value = () => "", Confirm = () => start = true },
            };
            int selected = items.Count - 1;
            float volume = -1f, nextVolumeCheck = 0f;
            while (!start)
            {
                if (calibrate)
                {
                    calibrate = false;
                    yield return LuminancePatch();
                    stage.SetBackground(MenuBackground);
                    PlaceText();
                }
                KeepTextInView();
                var step = input.StickStep;
                if (digit >= 0)
                {
                    digit = Mathf.Clamp(digit + step.x, 0, 5);
                    if (step.y != 0) p.UserID = StepDigit(p.UserID, digit, step.y);
                }
                else
                {
                    if (step.y != 0) selected = (selected - step.y + items.Count) % items.Count;
                    if (step.x != 0 && items[selected].Change != null) items[selected].Change(step.x);
                }
                if (input.ConfirmDown || input.TriggerDown)
                {
                    if (items[selected].Confirm != null) items[selected].Confirm();
                    else items[selected].Change(1);
                }
                if (Time.unscaledTime >= nextVolumeCheck)
                {
                    volume = DeviceInfo.MediaVolume();
                    nextVolumeCheck = Time.unscaledTime + 1f;
                }
                var sb = new StringBuilder("FOV Test\n\n");
                for (int i = 0; i < items.Count; i++)
                {
                    sb.Append(i == selected ? "> " : "   ").Append(items[i].Label);
                    var v = items[i].Value();
                    if (v != "") sb.Append(":  ").Append(v);
                    sb.Append('\n');
                }
                sb.Append('\n').Append(digit >= 0
                    ? "Stick left/right: digit   Stick up/down: change   A: done"
                    : "Stick up/down: select   left/right: change   A or trigger: choose");
                if (!lum.Known) sb.Append("\nLuminance: nominal Quest 3 curve, not measured");
                if (volume == 0f) sb.Append("\nHeadset volume is 0: no sounds will be heard");
                text.text = sb.ToString();
                yield return null;
            }
            a.HeadCursorVisible = t.ShowPointerWhileResponding;     // the log says what was shown
            settings.Save();
            text.text = "";
        }

        const float SizeIII = 0.43f, SizeV = 1.72f;

        static string SizeText(float deg) => deg.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + " deg" +
            (Mathf.Approximately(deg, SizeIII) ? " (standard)" : Mathf.Approximately(deg, SizeV) ? " (large)" : "");

        static string YesNo(bool b) => b ? "Yes" : "No";

        static string Step(List<string> options, string current, int d)
        {
            int i = Mathf.Max(0, options.IndexOf(current));
            return options[(i + d + options.Count) % options.Count];
        }

        static string IdText(int id, int digit)
        {
            var s = id.ToString("D6");
            return digit < 0 ? id.ToString() : s.Substring(0, digit) + "[" + s[digit] + "]" + s.Substring(digit + 1);
        }

        static int StepDigit(int id, int digit, int d)
        {
            int place = 1;
            for (int i = 0; i < 5 - digit; i++) place *= 10;
            int cur = id / place % 10;
            return id + ((cur + d + 10) % 10 - cur) * place;
        }

        /// <summary>Uniform full-field grey for a photometer; readings go into luminance.json.</summary>
        IEnumerator LuminancePatch()
        {
            float v = 1f;
            yield return null;
            while (!input.ConfirmDown && !input.TriggerDown)
            {
                v = Mathf.Clamp01(v + input.StickStep.x * 0.05f);
                stage.SetBackground(v);
                text.text = $"\n\n\n\n\n\n\n\n\n\n\nbrightness {v:0.00}   stick left/right, A to leave";
                yield return null;
            }
            yield return null;      // the closing press must not also act on the menu
        }

        // ---------------------------------------------------------------- test

        IEnumerator RunTest()
        {
            TestLayout layout = null;
            string error = null;
            try { layout = TestLayout.LoadBuiltin(settings.Activity.Layout); }
            catch (Exception e) { error = e.Message; }
            if (error != null)
            {
                yield return Message(error + "\n\nPress A to go back.");
                yield break;
            }
            layout.SetDiameter(settings.Test.StimulusDiameterDeg);
            var spots = layout.Select(settings.Activity.TestEyes, settings.Activity.IncludeBlindspot);
            session = new TestSession(settings, layout, stage, input, lum);
            var test = session.Test;
            test.Plan(spots.Count);
            stage.Configure(layout.Meta, spots[0].BackgroundCd);
            sounds.Volume = settings.Patient.SoundFXVolume;
            test.TargetLocked = sounds.Locked;
            test.Answered = o => { if (settings.Test.Feedback) sounds.Answer(o); };

            if (settings.Activity.TutorialEnabled)
            {
                float t0 = Time.unscaledTime;
                yield return Message(
                    "1.  Move your head to put the small dot on the target.\n" +
                    "A ring locks around it and lines divide the view into six areas.\n\n" +
                    "2.  Keep looking at the target. A faint spot flashes in one area.\n" +
                    "Move the dot into that area. If nothing appears, pull the trigger.\n\n" +
                    (settings.Test.Feedback ? "Green tick: right area.   Red cross: wrong area.   Bar: no answer.\n" : "") +
                    "The bar at the bottom shows how much is done.\n\n" +
                    "Press A for a short practice.");
                test.SetPrimaryToHead("practice start");
                yield return test.Practice(spots.Where(s => !s.IsBlindspot).ToList(), PracticeTrials);
                session.TutorialDurationSec = Time.unscaledTime - t0;
            }
            if (!test.Aborted)
            {
                yield return Message("The test starts now.\n\nLook straight ahead and press A.");
                text.text = "";
                test.SetPrimaryToHead("test start");
                yield return test.Run(spots);
            }
            stage.SetBackground(MenuBackground);
            var done = session;
            var saveError = done.Save();
            session = null;     // a pause on the results screen must not rewrite the saved log
            yield return Results(done, saveError);
        }

        IEnumerator Message(string msg)
        {
            PlaceText();
            text.text = msg;
            yield return null;
            while (!input.ConfirmDown)
            {
                KeepTextInView();
                yield return null;
            }
            text.text = "";
            yield return null;
        }

        IEnumerator Results(TestSession s, string saveError)
        {
            PlaceText();
            var sum = s.Summary();
            bool any = s.Test.History.Count > 0;
            var sb = new StringBuilder();
            sb.Append(s.Test.Aborted ? "Test stopped early\n\n" : s.Test.TimedOut ? "Time limit reached\n\n" : "Test finished\n\n");
            sb.Append($"Flash size {SizeText(settings.Test.StimulusDiameterDeg)}\n");
            sb.Append($"Seen {sum.SpotsSeen}   Missed {sum.SpotsMissed}   Not tested {sum.SpotsUntested}\n");
            if (!any) sb.Append("No trial was completed\n");
            if (any)
            {
                sb.Append($"Wrong direction {sum.NumIncorrectResponse}   Guess rate {sum.GuessRate:0.0%}\n");
                sb.Append($"Mean response time {sum.MeanSeenResponseTimeSec:0.00} s   Duration {sum.TestMinutes}:{sum.TestSeconds:00}\n");
                var blind = sum.TestLocations.Where(l => Mathf.Abs(l.LocationID) == 3000).ToList();
                if (blind.Count > 0)
                    sb.Append("Blind spot: " + string.Join(", ", blind.Select(b =>
                        $"{b.Eye} {(b.PercentCorrect > 0 ? "SEEN (check fixation)" : "not seen, as expected")}")) + "\n");
                DrawMap(sum);
            }
            sb.Append('\n').Append(saveError ?? "Saved: " + Path.GetFileName(s.LogPath));
            sb.Append("\n\nPress A for the menu.");
            text.text = sb.ToString();
            yield return null;
            while (!input.ConfirmDown) yield return null;
            text.text = "";
            ClearMap();
            yield return null;
        }

        /// <summary>Seen = bright dot, missed = dark dot; left eye on the left, right eye on the right.</summary>
        void DrawMap(SummaryLog sum)
        {
            var fwd = text.transform.forward;
            var up = text.transform.up;
            mapRoot = new GameObject("Map").transform;
            mapRoot.position = text.transform.position - fwd * 2f;
            foreach (var l in sum.TestLocations)
            {
                bool left = l.Eye == "Left";
                float cx = left ? -12f : 12f, cy = -16f;
                float x = cx + l.XDegrees * 0.38f, y = cy + l.YDegrees * 0.38f;
                var dir = (fwd + text.transform.right * Mathf.Tan(x * Mathf.Deg2Rad) + up * Mathf.Tan(y * Mathf.Deg2Rad)).normalized;
                var dot = new Spot("Map", mapRoot, spotShader, 5);
                dot.SetShape(0.35f, 0, 0.05f, 2f);
                dot.Place(dir * 2f, Vector3.zero, up);
                dot.Brightness = l.PercentCorrect > 0.5f ? 0.7f : l.PercentCorrect > 0f ? 0.4f : 0.02f;
                dot.Visible = true;
                map.Add(dot);
            }
        }

        void ClearMap()
        {
            map.Clear();
            if (mapRoot != null) Destroy(mapRoot.gameObject);
        }

        // ---------------------------------------------------------------- text

        void CreateText()
        {
            var go = new GameObject("Text");
            text = go.AddComponent<TextMesh>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.font = font;
            text.fontSize = 64;
            text.characterSize = 0.009f;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = new Color(MenuText, MenuText, MenuText, 1f);
            textMaterial = new Material(textShader) { mainTexture = font.material.mainTexture };
            go.GetComponent<MeshRenderer>().sharedMaterial = textMaterial;
            Font.textureRebuilt += f => { if (f == font && textMaterial != null) textMaterial.mainTexture = font.material.mainTexture; };
        }

        /// <summary>Bring the text back if it is out of view, e.g. the app started with the headset off.</summary>
        void KeepTextInView()
        {
            var to = text.transform.position - input.HeadPosition;
            if (Vector3.Angle(to, input.HeadForward) > 40f || Mathf.Abs(to.magnitude - 2f) > 0.5f) PlaceText();
        }

        /// <summary>Put the text 2 m ahead of where the head is facing now.</summary>
        void PlaceText()
        {
            var flat = Vector3.ProjectOnPlane(input.HeadForward, Vector3.up);
            if (flat.sqrMagnitude < 0.01f) flat = Vector3.forward;
            flat.Normalize();
            text.transform.position = input.HeadPosition + flat * 2f;
            text.transform.rotation = Quaternion.LookRotation(flat, Vector3.up);
        }
    }
}
