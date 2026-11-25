using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace FOVTest
{
    /// <summary>Test parameters the reference log does not contain. Change them in settings.json on the headset.</summary>
    public class TestParameters
    {
        /// <summary>Pointer travel from fixation that registers a response (the reference log does not say).</summary>
        public float ResponseThresholdDeg = 4f;
        /// <summary>Fixation targets appear within this radius of straight ahead (multifixation).</summary>
        public float FixationSpreadDeg = 8f;
        /// <summary>The next target is at least this far from the pointer, so it must be re-acquired.</summary>
        public float MinFixationStepDeg = 3f;
        public float FixationDotDiameterDeg = 0.9f;
        public float PointerDiameterDeg = 0.75f;
        /// <summary>The target locks when the pointer touches it while the head is slower than this, so that
        /// sweeping across it does not start a trial. Once locked it stays locked.</summary>
        public float LockMaxSpeedDegPerSec = 20f;
        /// <summary>A movement sooner than this after the stimulus was not caused by it; the trial is repeated.</summary>
        public float MinResponseSec = 0.15f;
        public float PreStimulusDelaySec = 0.1f;
        public float InterTrialSec = 0.7f;
        /// <summary>Flash diameter: 0.43 = Goldmann III (the reference), 1.72 = Goldmann V.</summary>
        public float StimulusDiameterDeg = 0.43f;
        /// <summary>Width of the raised-cosine edge; the stimulus keeps its nominal diameter at half height.</summary>
        public float StimulusEdgeDeg = 0.1f;
        /// <summary>Repeat a trial if its flash lost at least this many frames.</summary>
        public int MaxDroppedFrames = 2;
        public float DisplayFrequencyHz = 72f;
        public float RenderScale = 1.3f;
        public float StageDistanceM = 1.4f;
        public bool ShowPointerWhileResponding = true;
        /// <summary>Lines dividing the circle into six areas, from the moment the target is locked.</summary>
        public bool ShowDividers = true;
        /// <summary>Right / wrong / no answer mark and sound after each trial.</summary>
        public bool Feedback = true;
        public bool ShowProgress = true;
        public int SessionID = 0;
        public int SessionNumber = 0;
    }

    public class AppSettings
    {
        const int CurrentVersion = 2;
        public int Version = CurrentVersion;
        public PatientSettings Patient = new PatientSettings();
        public ActivitySettings Activity = new ActivitySettings();
        public TestParameters Test = new TestParameters();

        public static string FilePath => Path.Combine(Application.persistentDataPath, "settings.json");
        public static string LuminancePath => Path.Combine(Application.persistentDataPath, "luminance.json");
        public static string LogFolder => Path.Combine(Application.persistentDataPath, "Logs");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(FilePath)) return Parse(File.ReadAllText(FilePath));
            }
            catch (Exception e)
            {
                Debug.LogError("[FOVTest] settings.json is not readable, using defaults: " + e.Message);
            }
            return new AppSettings();
        }

        public static AppSettings Parse(string json)
        {
            var o = JObject.Parse(json);
            var s = o.ToObject<AppSettings>();
            if (((int?)o["Version"] ?? 1) < 2)
            {
                // Version 1 files hold the old, too small dots.
                var d = new TestParameters();
                s.Test.FixationDotDiameterDeg = d.FixationDotDiameterDeg;
                s.Test.PointerDiameterDeg = d.PointerDiameterDeg;
            }
            s.Version = CurrentVersion;
            return s;
        }

        public void Save()
        {
            try { File.WriteAllText(FilePath, JsonConvert.SerializeObject(this, Formatting.Indented)); }
            catch (Exception e) { Debug.LogError("[FOVTest] Could not save settings.json: " + e.Message); }
        }

        class LuminanceFile
        {
            public string Profile = "Quest3-measured";
            public bool Measured = true;
            public string Params = "";
        }

        /// <summary>Measured table from luminance.json if present, otherwise the nominal Quest 3 curve.</summary>
        public static LuminanceModel LoadLuminance()
        {
            try
            {
                if (File.Exists(LuminancePath))
                {
                    var f = JsonConvert.DeserializeObject<LuminanceFile>(File.ReadAllText(LuminancePath));
                    return LuminanceModel.Parse(f.Params, f.Profile, f.Measured);
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[FOVTest] luminance.json is not usable, using the nominal curve: " + e.Message);
            }
            return LuminanceModel.Quest3Nominal();
        }
    }
}
