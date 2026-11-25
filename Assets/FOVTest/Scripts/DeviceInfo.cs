using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.XR;

namespace FOVTest
{
    /// <summary>Fills the log's ClientInfo and time sections. Values Android does not expose to apps
    /// (serial number, MAC address) are null.</summary>
    public static class DeviceInfo
    {
        public static ClientInfo Collect(LuminanceModel lum, float displayHz)
        {
            var c = new ClientInfo();
            var hw = c.Hardware;
            hw.UniqueID = SystemInfo.deviceUniqueIdentifier;
            hw.AndroidBrightness = AndroidBrightness();
            hw.Processor = SystemInfo.processorType;
            hw.SystemMemory = SystemInfo.systemMemorySize;
            hw.GPUMemory = SystemInfo.graphicsMemorySize;
            hw.GPU = SystemInfo.graphicsDeviceName;
            hw.XRDevice = XRSettings.loadedDeviceName;
            hw.HMD = HmdName();
            hw.Model = SystemInfo.deviceModel;
            hw.Platform = "Quest";
            var devices = new List<InputDevice>();
            InputDevices.GetDevices(devices);
            foreach (var d in devices) hw.Joysticks.Add(d.name);
            hw.ActiveInput = "Touch";

            c.Software.OS = SystemInfo.operatingSystem;
            c.Software.Version = Application.version;
            c.Software.Package = Application.identifier;
            c.Software.Tags.Add("quest3");

            var d2 = c.DisplayInfo;
            d2.ID = SystemInfo.deviceModel;
            d2.Manufacturer = "Meta";
            d2.Model = hw.HMD;
            d2.PositionalTracking = true;
            d2.EyeTracking = false;
            d2.EyeResolutionH = XRSettings.eyeTextureWidth;
            d2.EyeResolutionV = XRSettings.eyeTextureHeight;
            d2.IPD = Ipd();
            d2.MinRefreshRate = Mathf.RoundToInt(displayHz);
            var cam = Camera.main;
            if (cam != null && d2.EyeResolutionV > 0) d2.PixelsPerDegree = cam.fieldOfView / d2.EyeResolutionV;
            d2.IsLuminanceDataKnown = lum.Known;
            d2.BlackLevel = lum.BlackLevel;
            d2.WhiteLevel = lum.WhiteLevel;
            d2.LuminanceFunctionParams = lum.ToLut();
            d2.LuminanceProfile = lum.Profile;
            return c;
        }

        static string HmdName()
        {
            var head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            return head.isValid ? head.name : SystemInfo.deviceModel;
        }

        static float Ipd()
        {
            try { return OVRPlugin.ipd * 1000f; }
            catch (Exception) { return 0f; }
        }

        /// <summary>Headset media volume, 0 to 1, or -1 if unknown.</summary>
        public static float MediaVolume()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var audio = activity.Call<AndroidJavaObject>("getSystemService", "audio");
                const int music = 3;
                return audio.Call<int>("getStreamVolume", music) / (float)audio.Call<int>("getStreamMaxVolume", music);
            }
            catch (Exception) { return -1f; }
#else
            return -1f;
#endif
        }

        static int AndroidBrightness()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var resolver = activity.Call<AndroidJavaObject>("getContentResolver");
                using var system = new AndroidJavaClass("android.provider.Settings$System");
                return system.CallStatic<int>("getInt", resolver, "screen_brightness", -1);
            }
            catch (Exception) { return -1; }
#else
            return -1;
#endif
        }

        public static TimeStartInfo TimeInfo(DateTime t, TimeSpan offset) => new TimeStartInfo
        {
            Year = t.Year, Month = t.Month, Day = t.Day, Hour24 = t.Hour,
            Hour12 = t.Hour % 12 == 0 ? 12 : t.Hour % 12,
            Minute = t.Minute, Second = t.Second,
            AMPM = t.Hour < 12 ? "AM" : "PM",
            UTCOffset = offset.TotalHours == Math.Floor(offset.TotalHours)
                ? (object)(int)offset.TotalHours : offset.TotalHours,
            FriendlyDate = t.ToString("MMM d, yyyy '@' h:mm:ss tt", CultureInfo.InvariantCulture),
        };
    }
}
