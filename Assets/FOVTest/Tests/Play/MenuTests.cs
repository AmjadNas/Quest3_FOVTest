using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FOVTest.Tests
{
    /// <summary>Button presses that last exactly one frame, like OVRInput.GetDown.</summary>
    public class ScriptedInput : HeadInput
    {
        int confirmFrame = -1, stickFrame = -1;
        Vector2Int stick;
        public Quaternion Head = Quaternion.identity;
        public bool Worn = true;

        public override Vector3 HeadPosition => Vector3.zero;
        public override Quaternion HeadRotation => Head;
        public override Vector3 EyePosition(EyeMask e) => Vector3.zero;
        public override bool Active => Worn;
        public override bool TriggerDown => false;
        public override bool ConfirmDown => Time.frameCount == confirmFrame;
        public override bool AbortHeld => false;
        public override Vector2Int StickStep => Time.frameCount == stickFrame ? stick : Vector2Int.zero;

        public IEnumerator Confirm()
        {
            confirmFrame = Time.frameCount + 1;
            for (int i = 0; i < 4; i++) yield return null;
        }

        public IEnumerator Stick(int x, int y)
        {
            stick = new Vector2Int(x, y);
            stickFrame = Time.frameCount + 1;
            for (int i = 0; i < 4; i++) yield return null;
        }
    }

    public class MenuTests
    {
        GameObject go, camGo;

        string settingsBackup;

        [SetUp]
        public void KeepSettings()
        {
            settingsBackup = System.IO.File.Exists(AppSettings.FilePath) ? System.IO.File.ReadAllText(AppSettings.FilePath) : null;
            System.IO.File.Delete(AppSettings.FilePath);        // every test starts from the defaults
        }

        [TearDown]
        public void Clean()
        {
            if (settingsBackup != null) System.IO.File.WriteAllText(AppSettings.FilePath, settingsBackup);
            else System.IO.File.Delete(AppSettings.FilePath);
            for (var s = GameObject.Find("Stage"); s != null; s = GameObject.Find("Stage")) Object.DestroyImmediate(s);
            Object.Destroy(go);
            Object.Destroy(camGo);
            foreach (var t in Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None)) Object.Destroy(t.gameObject);
        }

        IEnumerator Launch(System.Action<ScriptedInput> ready, bool worn = true)
        {
            camGo = new GameObject("cam") { tag = "MainCamera" };
            camGo.AddComponent<Camera>();
            go = new GameObject("app");
            var input = go.AddComponent<ScriptedInput>();
            input.Worn = worn;
            var app = go.AddComponent<AppController>();
            app.SetShaders(Shader.Find("FOVTest/Spot"), Shader.Find("FOVTest/Text"));
            app.UseInput(input);
            for (int i = 0; i < 5; i++) yield return null;
            ready(input);
        }

        static string Screen => Object.FindAnyObjectByType<TextMesh>().text;
        static Vector3 TextDirection => Object.FindAnyObjectByType<TextMesh>().transform.position.normalized;
        static Vector3 Facing(float yaw, float pitch) => Quaternion.Euler(-pitch, yaw, 0) * Vector3.forward;

        static IEnumerator Wait(int frames)
        {
            for (int i = 0; i < frames; i++) yield return null;
        }

        static IEnumerator Select(ScriptedInput input, string label)
        {
            for (int i = 0; i < 30 && !Screen.Contains("> " + label); i++) yield return input.Stick(0, 1);
            StringAssert.Contains("> " + label, Screen);
        }

        /// <summary>Menu to "The test starts now", without the practice.</summary>
        static IEnumerator ToStartMessage(ScriptedInput input)
        {
            yield return Select(input, "Instructions and practice");
            yield return input.Stick(1, 0);
            StringAssert.Contains("Instructions and practice:  No", Screen);
            yield return Select(input, "START TEST");
            yield return input.Confirm();
            StringAssert.Contains("The test starts now", Screen);
        }

        [UnityTest]
        public IEnumerator StartMessage_FollowsTheHead_AndTheTestIsCenteredWhereTheHeadFaces()
        {
            ScriptedInput input = null;
            yield return Launch(i => input = i);
            yield return ToStartMessage(input);
            foreach (var (yaw, pitch) in new[] { (35f, -10f), (-60f, 5f), (12f, -18f) })
            {
                input.Head = Quaternion.Euler(-pitch, yaw, 0);
                yield return Wait(3);
                Assert.Less(Vector3.Angle(TextDirection, Facing(yaw, pitch)), 0.5f, "the text must not pull the head anywhere");
            }
            yield return input.Confirm();
            var target = GameObject.Find("Fixation").transform.position.normalized;
            Assert.Less(Vector3.Angle(target, Facing(12f, -18f)), 8.1f, "targets around the direction faced when pressing A");
        }

        [UnityTest]
        public IEnumerator MenuText_IsPlacedAgain_WhenTrackingStartsAfterLaunch()
        {
            ScriptedInput input = null;
            yield return Launch(i => input = i, worn: false);      // no real head pose yet
            input.Head = Quaternion.Euler(0, 20f, 0);               // within the follow limit, so only tracking start moves it
            input.Worn = true;
            yield return Wait(3);
            Assert.Less(Vector3.Angle(TextDirection, Facing(20f, 0f)), 0.5f);
        }

        [UnityTest]
        public IEnumerator MenuText_StaysWhileReading_AndFollowsALargerTurn()
        {
            ScriptedInput input = null;
            yield return Launch(i => input = i);
            yield return Wait(2);
            var at = TextDirection;
            input.Head = Quaternion.Euler(0, 20f, 0);               // scanning along a line
            yield return Wait(3);
            Assert.Less(Vector3.Angle(TextDirection, at), 0.1f);
            input.Head = Quaternion.Euler(-15f, 30f, 0);
            yield return Wait(3);
            Assert.AreEqual(30f, HeadAngles.Yaw(TextDirection), 0.5f);
            Assert.AreEqual(0f, HeadAngles.Pitch(TextDirection), 0.5f, "the menu stays at eye level");
        }

        [UnityTest]
        public IEnumerator LuminancePatch_ClosesWithA()
        {
            ScriptedInput input = null;
            yield return Launch(i => input = i);
            for (int i = 0; i < 20 && !Screen.Contains("> Luminance patch"); i++) yield return input.Stick(0, 1);
            StringAssert.Contains("> Luminance patch", Screen);
            yield return input.Confirm();
            StringAssert.Contains("brightness", Screen);
            yield return input.Confirm();
            StringAssert.Contains("FOV Test", Screen, "A must return to the menu");
            for (int i = 0; i < 10; i++) yield return null;
            StringAssert.DoesNotContain("brightness", Screen, "the patch must stay closed");
            // and it can be opened again
            yield return input.Confirm();
            StringAssert.Contains("brightness", Screen);
        }

        [UnityTest]
        public IEnumerator LuminancePatch_StickChangesTheGreyLevel()
        {
            ScriptedInput input = null;
            yield return Launch(i => input = i);
            for (int i = 0; i < 20 && !Screen.Contains("> Luminance patch"); i++) yield return input.Stick(0, 1);
            yield return input.Confirm();
            Assert.AreEqual(1f, Camera.main.backgroundColor.g, 1e-3f);
            for (int i = 0; i < 4; i++) yield return input.Stick(-1, 0);
            Assert.AreEqual(0.8f, Camera.main.backgroundColor.g, 1e-3f);
            StringAssert.Contains("brightness 0.80", Screen);
            yield return input.Stick(1, 0);
            Assert.AreEqual(0.85f, Camera.main.backgroundColor.g, 1e-3f);
        }
    }
}
