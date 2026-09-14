using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using ANL.UI;

namespace ANL.Editor
{
    public static class ProjectBootstrap
    {
        private const string ScenePath = "Assets/_Game/Scenes/Main.unity";

        [MenuItem("ANL/Bootstrap Project")]
        public static void BootstrapFromMenu()
        {
            Bootstrap();
        }

        public static void BatchBootstrap()
        {
            try
            {
                Bootstrap();
                EditorApplication.Exit(0);
            }
            catch (System.Exception exception)
            {
                Debug.LogError("[ANL] bootstrap failed: " + exception);
                EditorApplication.Exit(1);
            }
        }

        public static void Bootstrap()
        {
            ConfigurePlayer();
            ArtImporter.ImportAll();
            CatalogBuilder.BuildAll();
            CreateMainScene();
            Debug.Log("[ANL] bootstrap complete");
        }

        private static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "ANL Studio";
            PlayerSettings.productName = "AI Never Lies";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, "com.anlstudio.aineverlies");
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.runInBackground = false;
            PlayerSettings.visibleInBackground = true;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            var projectSettings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (projectSettings != null && projectSettings.Length > 0)
            {
                var serialized = new SerializedObject(projectSettings[0]);
                var handler = serialized.FindProperty("activeInputHandler");
                if (handler != null)
                {
                    handler.intValue = 1;
                    serialized.ApplyModifiedProperties();
                }
            }
        }

        private static void CreateMainScene()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            if (File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                EnsureCameraAndRoot();
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            else
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EnsureCameraAndRoot();
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        private static void EnsureCameraAndRoot()
        {
            var camera = Object.FindFirstObjectByType<Camera>();
            if (camera == null)
            {
                var cameraGo = new GameObject("Main Camera");
                cameraGo.tag = "MainCamera";
                camera = cameraGo.AddComponent<Camera>();
            }
            camera.orthographic = true;
            camera.orthographicSize = 5.4f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.05f, 0.07f, 1f);
            camera.transform.position = new Vector3(0f, 0f, -10f);

            var appRoot = Object.FindFirstObjectByType<AppRoot>();
            if (appRoot == null)
            {
                var root = new GameObject("AppRoot");
                root.AddComponent<AppRoot>();
            }
        }
    }
}
