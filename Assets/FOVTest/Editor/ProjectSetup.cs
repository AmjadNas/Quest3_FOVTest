using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;

namespace FOVTest.EditorTools
{
    /// <summary>Configures the project for Quest 3 (OpenXR + Meta XR Core SDK), builds the scene and the APK.</summary>
    public static class ProjectSetup
    {
        const string ScenePath = "Assets/FOVTest/Scenes/FOVTest.unity";
        const string ApkPath = "Builds/FOVTest.apk";

        [MenuItem("FOVTest/Set up project")]
        public static void Run()
        {
            Player();
            Xr();
            Meta();
            Scene();
            AssetDatabase.SaveAssets();
            Debug.Log("[FOVTest] Project setup finished");
        }

        [MenuItem("FOVTest/Build APK")]
        public static void BuildApk()
        {
            Run();
            Directory.CreateDirectory("Builds");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = ApkPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            });
            var r = report.summary;
            Debug.Log($"[FOVTest] Build {r.result}: {r.totalErrors} errors, {r.totalSize / 1048576} MB, {r.outputPath}");
            if (r.result != UnityEditor.Build.Reporting.BuildResult.Succeeded && Application.isBatchMode)
                EditorApplication.Exit(1);
        }

        static void Player()
        {
            PlayerSettings.companyName = "FOVTest";
            PlayerSettings.productName = "FOV Test";
            PlayerSettings.bundleVersion = "1.0.0";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.fovtest.perimetry");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)32;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.forceInternetPermission = false;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            PlayerSettings.colorSpace = ColorSpace.Linear;      // sRGB eye buffer: grey levels are written exactly
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.gpuSkinning = false;
            PlayerSettings.SetMobileMTRendering(NamedBuildTarget.Android, true);
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
            EditorUserBuildSettings.buildAppBundle = false;

            // One quality level with MSAA 4x and nothing adaptive.
            QualitySettings.antiAliasing = 4;
            QualitySettings.vSyncCount = 0;
            QualitySettings.realtimeReflectionProbes = false;
            QualitySettings.shadows = ShadowQuality.Disable;
        }

        static void Xr()
        {
            var group = BuildTargetGroup.Android;
            if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget perTarget) || perTarget == null)
            {
                perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                Directory.CreateDirectory("Assets/XR");
                AssetDatabase.CreateAsset(perTarget, "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perTarget, true);
            }
            if (!perTarget.HasSettingsForBuildTarget(group)) perTarget.CreateDefaultSettingsForBuildTarget(group);
            if (!perTarget.HasManagerSettingsForBuildTarget(group)) perTarget.CreateDefaultManagerSettingsForBuildTarget(group);
            var manager = perTarget.ManagerSettingsForBuildTarget(group);
            if (!XRPackageMetadataStore.IsLoaderAssigned(typeof(OpenXRLoader).FullName, group) &&
                !XRPackageMetadataStore.AssignLoader(manager, typeof(OpenXRLoader).FullName, group))
                throw new Exception("Could not assign the OpenXR loader for Android.");
            EditorUtility.SetDirty(perTarget);

            FeatureHelpers.RefreshFeatures(group);
            Enable(group, "com.unity.openxr.feature.metaquest", true);
            Enable(group, "com.meta.openxr.feature.metaxr", true);
            Enable(group, "com.unity.openxr.feature.input.oculustouch", true);
            Enable(group, "com.unity.openxr.feature.input.metaquestplus", true);
            // Foveation must stay off: it lowers resolution in the peripheral field under test.
            Enable(group, "com.meta.openxr.feature.foveation", false);
            Enable(group, "com.unity.openxr.feature.foveatedrendering", false);
            Enable(group, "com.meta.openxr.feature.subsampledLayout", false);
            Enable(group, "com.meta.openxr.feature.spacewarp", false);

            var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(group);
            settings.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
            EditorUtility.SetDirty(settings);
        }

        static void Enable(BuildTargetGroup group, string id, bool on)
        {
            var f = FeatureHelpers.GetFeatureWithIdForBuildTarget(group, id);
            if (f == null)
            {
                if (on) throw new Exception("OpenXR feature not found: " + id);
                return;
            }
            f.enabled = on;
            if (f is UnityEngine.XR.OpenXR.Features.MetaQuestSupport.MetaQuestFeature quest)
            {
                quest.ForceRemoveInternetPermission = true;     // the app works offline and sends nothing
                // Quest Pro is targeted by default, which makes eye-tracking hardware a requirement.
                var so = new SerializedObject(quest);
                var devices = so.FindProperty("targetDevices");
                for (int i = 0; i < devices.arraySize; i++)
                {
                    var d = devices.GetArrayElementAtIndex(i);
                    var name = d.FindPropertyRelative("manifestName").stringValue;
                    d.FindPropertyRelative("enabled").boolValue = name == "eureka" || name == "quest3" || name == "quest3s";
                }
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorUtility.SetDirty(f);
        }

        static void Meta()
        {
            var cfg = OVRProjectConfig.CachedProjectConfig;
            cfg.targetDeviceTypes = new List<OVRProjectConfig.DeviceType>
                { OVRProjectConfig.DeviceType.Quest3, OVRProjectConfig.DeviceType.Quest3S };
            OVRProjectConfig.CommitProjectConfig(cfg);
        }

        static void Scene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.black;
            RenderSettings.fog = false;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.meta.xr.sdk.core/Prefabs/OVRCameraRig.prefab");
            if (prefab == null) throw new Exception("OVRCameraRig prefab not found in the Meta XR Core SDK.");
            var rig = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            var ovr = rig.GetComponent<OVRManager>();
            ovr.trackingOriginType = OVRManager.TrackingOrigin.EyeLevel;
            foreach (var cam in rig.GetComponentsInChildren<Camera>(true))
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                cam.allowHDR = false;
            }

            var app = new GameObject("FOVTest").AddComponent<AppController>();
            var spot = AssetDatabase.LoadAssetAtPath<Shader>("Assets/FOVTest/Shaders/Spot.shader");
            var text = AssetDatabase.LoadAssetAtPath<Shader>("Assets/FOVTest/Shaders/Text.shader");
            if (spot == null || text == null) throw new Exception("Shaders not found.");
            app.SetShaders(spot, text);
            EditorUtility.SetDirty(app);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }
    }
}
