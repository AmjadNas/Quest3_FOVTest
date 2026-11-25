using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace FOVTest
{
    /// <summary>One test from start to saved log. The log is saved at the end and whenever the app is paused,
    /// so a run that is interrupted is kept (untested spots are counted as SpotsUntested).</summary>
    public class TestSession
    {
        readonly AppSettings settings;
        readonly TestLayout layout;
        readonly Stage stage;
        readonly LogHeader header;
        public readonly PerimetryTest Test;
        public float TutorialDurationSec;
        public string LogPath { get; }
        /// <summary>Same name as the log, in a folder of its own (LogsExtra), so that Logs holds only the reference format.</summary>
        public string ExtraPath { get; }

        public TestSession(AppSettings settings, TestLayout layout, Stage stage, HeadInput input, LuminanceModel lum,
            string folder = null)
        {
            this.settings = settings;
            this.layout = layout;
            this.stage = stage;
            Test = new PerimetryTest(stage, input, settings, layout);
            var now = DateTimeOffset.Now;
            header = new LogHeader
            {
                UserID = settings.Patient.UserID,
                SessionID = settings.Test.SessionID,
                SessionNumber = settings.Test.SessionNumber,
                PatientSettings = settings.Patient,
                ActivitySettings = settings.Activity,
                UnixTimeStart = now.ToUnixTimeSeconds(),
                TimeStart = DeviceInfo.TimeInfo(now.DateTime, now.Offset),
                ClientInfo = DeviceInfo.Collect(lum, settings.Test.DisplayFrequencyHz),
            };
            folder = folder ?? AppSettings.LogFolder;
            var name = $"{header.UnixTimeStart}_{header.UserID}_VisualField.json";
            LogPath = Path.Combine(folder, name);
            ExtraPath = Path.Combine(folder + "Extra", name);
            Test.Extra.Log = name;
        }

        public SummaryLog Summary()
        {
            var end = DateTimeOffset.Now;
            return SummaryBuilder.Build(Test.History, Test.TotalLocations, Test.DurationSec, TutorialDurationSec,
                stage.BackgroundBrightness, layout.Meta.ResponseTimeoutSec, layout, end.DateTime, end.ToUnixTimeSeconds());
        }

        // The extra file must never cost the log itself.
        void SaveExtra()
        {
            try { LogWriter.Save(ExtraPath, JsonConvert.SerializeObject(Test.Extra, Formatting.Indented)); }
            catch (Exception e) { Debug.LogException(e); }
        }

        /// <summary>Returns an error message, or null when the log was saved.</summary>
        public string Save()
        {
            if (!Test.Started) return "The test was not started, so there is nothing to save.";
            try
            {
                var s = Summary();
                LogWriter.Save(LogPath, LogWriter.ToJson(header, Test.History, s, s.TestUnixTime));
                Debug.Log("[FOVTest] Log saved: " + LogPath);
                SaveExtra();
                return null;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return "The log could not be saved: " + e.Message;
            }
        }
    }
}
