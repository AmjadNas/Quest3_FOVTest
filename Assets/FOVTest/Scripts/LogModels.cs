using System.Collections.Generic;
using Newtonsoft.Json;

namespace FOVTest
{
    // Field names, order and types follow the reference VisualField.json (DataVersion 11).

    public class Vec3
    {
        public float x, y, z;
    }

    public class PatientSettings
    {
        public int UserID = 0;
        public string Name = "";
        public int Age = 0;
        public int DOB = 1;
        public bool IsDemo = false;
        public string DominantEye = "Right";
        public Vec3 AngleOfDeviation = new Vec3();
        public bool SplitDeviation = true;
        public bool EnableCameraFilters = true;
        public float ContrastRatio = 0;
        public float Occlusion = 0;
        public float OccluderBrightness = 20;
        public float Blur = 0;
        public float Luminance = 100;
        public float OcclusionLeft = 0;
        public float OcclusionRight = 0;
        public float BlurLeft = 0;
        public float BlurRight = 0;
        public string MedicalRecordNumber = "";
        public float SoundFXVolume = 50;
        public float MusicVolume = 10;
        public float GamepadSensitivity = 35;
        public int AntiAliasing = 4;
        public bool IsLeapEnabled = false;
        public bool SpinningLogo = false;
        public bool UseZeroIPD = false;
        public string Language = "English";
    }

    public class ActivitySettings
    {
        public bool TutorialEnabled = true;
        public string Layout = "Training-30deg";
        public string FixationTargetLaterality = "Monocular";
        public string ResponseMode = "Head";
        public string TestEyes = "Bilateral";
        public bool FixationAssist = false;
        public bool IncludeBlindspot = true;
        public bool Stereoperimetry = false;
        public bool FixationRingVisible = false;
        public bool EyeCursorVisible = false;
        public bool HeadCursorVisible = false;
        public string CSVPath = "";
    }

    public class TimeStartInfo
    {
        public int Year, Month, Day, Hour24, Hour12, Minute, Second;
        public string AMPM;
        public object UTCOffset;
        public string FriendlyDate;
    }

    public class HardwareInfo
    {
        public string UniqueID;
        public string AndroidSerial;
        public int AndroidBrightness;
        public string Processor;
        public int SystemMemory;
        public int GPUMemory;
        public string GPU;
        public string MACAddress;
        public string XRDevice;
        public string HMD;
        public string Model;
        public string Platform;
        public List<string> Joysticks = new List<string>();
        public string ActiveInput;
    }

    public class SoftwareInfo
    {
        public string Source = "Unity";
        public string OS;
        public string CommitID = "";
        public string Version;
        public string Package;
        public List<string> Tags = new List<string>();
    }

    public class DisplayInfo
    {
        public string ID;
        public string Manufacturer;
        public string Model;
        public bool PositionalTracking;
        public bool EyeTracking;
        public int EyeResolutionH;
        public int EyeResolutionV;
        public float IPD;
        public int MinRefreshRate;
        public float PixelsPerDegree;       // degrees per pixel, as in the reference log (0.0703125 = 90/1280)
        public bool IsLuminanceDataKnown;
        public float BlackLevel;
        public float WhiteLevel;
        public string LuminanceFunctionType = "Interpolate";
        public string LuminanceFunctionParams;
        public int LuminanceSetVersion = 1;
        public string LuminanceProfile;
    }

    public class ClientInfo
    {
        public HardwareInfo Hardware = new HardwareInfo();
        public SoftwareInfo Software = new SoftwareInfo();
        public DisplayInfo DisplayInfo = new DisplayInfo();
    }

    public class LogHeader
    {
        public int UserID;
        public int SessionID;
        public int SessionNumber;
        public int ActivityPosition;
        public string SceneName = "VisualField";
        public object SceneParameters = null;
        public PatientSettings PatientSettings;
        public ActivitySettings ActivitySettings;
        public int PlayDuration;
        public long UnixTimeStart;
        public TimeStartInfo TimeStart;
        public ClientInfo ClientInfo;
        public string CommitID;
        public int DataVersion = 11;
    }

    public class SpotLog
    {
        public float XDegrees, YDegrees;
        public string Color;
        public float ContrastEst;
        public float BgLuminanceCdEst;
        public string Eye;
        public float StimDiamDeg;
        public float StimDurationSec;
        public int Index;
        public string SpotType = "Normal";
        public int SpotId;
        public int GroupId;
        public int LocationID;
        public string Note;
    }

    public class TrialLog
    {
        public SpotLog Spot;
        public float ElapsedTimeSec;
        public int TrialIndex;
        public float TimePresented;
        public float ResponseTimeSec;
        public float FixationStart;
        public float FixationDurationSec;
        public float TotalTrialDurationSec;
        public int ChoiceIndex;
        public bool Seen;
        public bool Responded;
        public int FramesPresented;
        public int MeasuredFlashDurationMs;
        public bool TriggerPull;
        public float PercentComplete;

        /// <summary>Not counted and repeated later. In the file this shows as a repeated TrialIndex.</summary>
        [JsonIgnore] public bool Invalid;
    }

    public class TestLocationLog
    {
        public int LocationID;
        public string Eye;
        public float XDegrees, YDegrees;
        public float PercentCorrect;
        public List<int> TrialIndices = new List<int>();
    }

    public class SummaryLog
    {
        public string TestDate;
        public long TestUnixTime;
        public int TestMinutes;
        public int TestSeconds;
        public float TutorialDurationSec;
        public float TestDurationSec;
        public float TotalProcedureDurationSec;
        public float BackgroundBrightness;
        public List<float> TestContrasts = new List<float>();
        public int NumStimuliPresented;
        public int NumInitialMiss;
        public int NumLocationsTested;
        public int SpotsMissed;
        public int SpotsSeen;
        public int SpotsUntested;
        public int NumIncorrectResponse;
        public float GuessRate;
        public float PercentCorrect;
        public float MeanTrialDurationSec;
        public float MeanSeenResponseTimeSec;
        public float MeanIncorrectResponseTimeSec;
        public float MeanNoResponseTimeSec;
        public string Layout;
        public List<TestLocationLog> TestLocations = new List<TestLocationLog>();
        public float Reliability;
        public float WaitTimeBeforeMissedSec;
        public int LayoutVersion;
        public string LayoutData;
        public string LayoutMetaJson;
        public string LayoutDescription;
        public float UIVersion = 0.2f;
        public long AppTimeStartMs;
        public long UnixTimeLoad;
        public long UnixTimeStart;
        public long UnixTimeEnd;
        public long DurationMs;
    }
}
