using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Kendo.EditorTools.Setup
{
    /// <summary>Desktop player build of the nine scenes (Kendo ▸ Build Desktop Player, or batch mode with -target win|linux|mac).</summary>
    public static class KendoPlayerBuild
    {
        [MenuItem("Kendo/Build Desktop Player (Windows)", priority = -90)]
        public static void BuildWindows() => Run(BuildTarget.StandaloneWindows64, "Builds/Windows/KendoMastersLegacy.exe");

        public static void Build()
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-target");
            string t = i >= 0 && i + 1 < args.Length ? args[i + 1] : "win";
            var ok = t == "linux" ? Run(BuildTarget.StandaloneLinux64, "Builds/Linux/KendoMastersLegacy.x86_64")
                   : t == "mac" ? Run(BuildTarget.StandaloneOSX, "Builds/macOS/KendoMastersLegacy.app")
                   : Run(BuildTarget.StandaloneWindows64, "Builds/Windows/KendoMastersLegacy.exe");
            EditorApplication.Exit(ok ? 0 : 1);
        }

        private static bool Run(BuildTarget target, string path)
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0) { Debug.LogError("[Kendo] No scenes in the build list – run Kendo ▸ Build Game first."); return false; }
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = scenes, locationPathName = path, target = target, options = BuildOptions.None });
            Debug.Log($"[Kendo] Player build {report.summary.result}: {path} ({report.summary.totalSize / (1024 * 1024)} MB)");
            return report.summary.result == BuildResult.Succeeded;
        }
    }
}
