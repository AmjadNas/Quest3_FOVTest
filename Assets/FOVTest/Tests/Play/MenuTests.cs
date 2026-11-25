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

        public override Vector3 HeadPosition => Vector3.zero;
        public override Quaternion HeadRotation => Quaternion.identity;
        public override Vector3 EyePosition(EyeMask e) => Vector3.zero;
        public override bool Active => true;
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

        [TearDown]
        public void Clean()
        {
            Object.Destroy(go);
            Object.Destroy(camGo);
            foreach (var t in Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None)) Object.Destroy(t.gameObject);
        }

        IEnumerator Launch(System.Action<ScriptedInput> ready)
        {
            camGo = new GameObject("cam") { tag = "MainCamera" };
            camGo.AddComponent<Camera>();
            go = new GameObject("app");
            var input = go.AddComponent<ScriptedInput>();
            var app = go.AddComponent<AppController>();
            app.SetShaders(Shader.Find("FOVTest/Spot"), Shader.Find("FOVTest/Text"));
            app.UseInput(input);
            for (int i = 0; i < 5; i++) yield return null;
            ready(input);
        }

        static string Screen => Object.FindAnyObjectByType<TextMesh>().text;

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
