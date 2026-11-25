using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

namespace FOVTest
{
    /// <summary>On-device check without a wearer. Starts only when the app is launched over adb with
    /// "--ez selftest true". Shows a held stimulus to each eye in turn (for screenshots), then runs the 30-degree
    /// layout with a simulated participant and saves to the SelfTest folder, never to Logs.</summary>
    public static class SelfTest
    {
        public static bool Requested
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                try
                {
                    using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                    using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                    using var intent = activity.Call<AndroidJavaObject>("getIntent");
                    return intent.Call<bool>("getBooleanExtra", "selftest", false);
                }
                catch (Exception) { return false; }
#else
                return false;
#endif
            }
        }

        public static IEnumerator Run(GameObject host, Stage stage, HeadInput body, AppSettings userSettings,
            LuminanceModel lum, Action<string> show)
        {
            show("");
            var settings = new AppSettings { Test = userSettings.Test };    // never saved
            settings.Patient.IsDemo = true;
            settings.Patient.Name = "SelfTest";
            var layout = TestLayout.LoadBuiltin("Training-30deg");
            var sim = host.AddComponent<SimulatedParticipant>();
            sim.Body = body;
            sim.Stage = stage;
            stage.Configure(layout.Meta, layout.Spots[0].BackgroundCd);

            foreach (var eye in new[] { EyeMask.Left, EyeMask.Right })
            {
                var spot = layout.Spots.First(s => s.Eye == eye.ToString() && Mathf.Approximately(s.X, 3.5355339f) && s.Y > 0);
                stage.ShowFixation(body.HeadForward, body.HeadForward, EyeMask.Both, false, 0.5f);
                stage.UpdatePointer(body.HeadForward, body.HeadUp, false);
                stage.ShowStimulus(spot, body.EyePosition(eye), body.HeadUp);
                Debug.Log("[FOVTest] selftest holding stimulus, eye " + eye);
                yield return new WaitForSecondsRealtime(6f);
                stage.HideTarget();
            }

            var session = new TestSession(settings, layout, stage, sim, lum,
                Path.Combine(Application.persistentDataPath, "SelfTest"));
            sim.Face(body.HeadForward);
            session.Test.SetPrimaryToHead();
            sim.Test = session.Test;
            Debug.Log("[FOVTest] selftest run starts");
            yield return session.Test.Run(layout.Select("Bilateral", true));
            var error = session.Save();
            var h = session.Test.History;
            Debug.Log($"[FOVTest] selftest done: {h.Count} trials, {h.Count(t => t.Invalid)} repeated for dropped frames, " +
                      $"{h.Count(t => t.Seen)} seen, frames {h.Min(t => t.FramesPresented)}-{h.Max(t => t.FramesPresented)}, " +
                      $"flash {h.Min(t => t.MeasuredFlashDurationMs)}-{h.Max(t => t.MeasuredFlashDurationMs)} ms, save {error ?? "ok"}");
            show("Self-test finished");
        }
    }
}
