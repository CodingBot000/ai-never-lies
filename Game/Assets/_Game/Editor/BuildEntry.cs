using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ANL.Editor
{
    public static class BuildEntry
    {
        private const string DevelopmentOutput = "Builds/macOS/AINeverLiesDev.app";
        private const string ReleaseOutput = "Builds/macOS/AINeverLies.app";

        [MenuItem("ANL/Build/Host Development")]
        public static void BuildHostDevelopment()
        {
            Build(DevelopmentOutput, true, "ANL_TEST_TOOLS", "development");
        }

        [MenuItem("ANL/Build/Host Release")]
        public static void BuildHostRelease()
        {
            Build(ReleaseOutput, false, null, "release");
        }

        public static void BuildHostDevelopmentBatch()
        {
            BuildHostDevelopment();
        }

        public static void BuildHostReleaseBatch()
        {
            BuildHostRelease();
        }

        private static void Build(string output, bool development, string define, string label)
        {
            var absolute = Path.GetFullPath(Path.Combine(ProjectPaths.Root, output));
            var directory = Path.GetDirectoryName(absolute);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            var previous = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Standalone);
            var defines = string.IsNullOrEmpty(previous) ? define : (string.IsNullOrEmpty(define) ? previous : previous + ";" + define);
            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Standalone, defines ?? "");
            try
            {
                var options = new BuildPlayerOptions
                {
                    scenes = new[] { "Assets/_Game/Scenes/Main.unity" },
                    locationPathName = absolute,
                    target = BuildTarget.StandaloneOSX,
                    options = development ? BuildOptions.Development | BuildOptions.AllowDebugging : BuildOptions.None
                };
                var report = BuildPipeline.BuildPlayer(options);
                var summary = report.summary;
                Debug.Log($"[ANL] {label} build: result={summary.result} size={summary.totalSize} time={summary.totalTime} output={absolute}");
                if (summary.result != BuildResult.Succeeded)
                {
                    Debug.LogError($"[ANL] {label} build FAILED: {summary.result}");
                    EditorApplication.Exit(1);
                    return;
                }
                if (!File.Exists(Path.Combine(absolute, "Contents", "MacOS", "AINeverLies")) &&
                    !File.Exists(Path.Combine(absolute, "Contents", "MacOS", "AINeverLiesDev")))
                {
                    Debug.LogWarning("[ANL] build succeeded but the expected executable name was not found; check the bundle contents");
                }
                EditorApplication.Exit(0);
            }
            finally
            {
                PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Standalone, previous ?? "");
            }
        }
    }
}
