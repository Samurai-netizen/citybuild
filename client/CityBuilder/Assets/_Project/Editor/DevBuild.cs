using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CityBuilder.Client.Editor
{
    /// <summary>
    /// Makes a development player (M0-82, <c>docs/BUILD.md</c>): the enabled scenes of the build settings (Bootstrap only), the
    /// <see cref="BuildOptions.Development"/> flag (plain http and the stored dev token work only in such a player), output under
    /// <c>Builds/</c>. From the menu <c>CityBuilder → Development build</c>, or headless:
    /// <c>Unity -batchmode -quit -projectPath client/CityBuilder -buildTarget &lt;target&gt; -executeMethod CityBuilder.Client.Editor.DevBuild.BuildFromCommandLine</c>.
    /// </summary>
    public static class DevBuild
    {
        /// <summary>The options of a development build of <paramref name="target"/> into <paramref name="outputPath"/>.</summary>
        /// <param name="target">The platform.</param>
        /// <param name="outputPath">The player's path, relative to the project folder.</param>
        /// <returns>The build options.</returns>
        public static BuildPlayerOptions Options(BuildTarget target, string outputPath)
        {
            return new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
                target = target,
                targetGroup = BuildPipeline.GetBuildTargetGroup(target),
                locationPathName = outputPath,
                options = BuildOptions.Development,
            };
        }

        /// <summary>Where a development build of <paramref name="target"/> goes, under <c>Builds/</c> (git ignores it).</summary>
        /// <param name="target">The platform.</param>
        /// <returns>The output path, relative to the project folder.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The target is not one <c>docs/BUILD.md</c> covers.</exception>
        public static string DefaultOutput(BuildTarget target)
        {
            return target switch
            {
                BuildTarget.StandaloneOSX => "Builds/macOS/CityBuilder.app",
                BuildTarget.StandaloneWindows64 => "Builds/Windows/CityBuilder.exe",
                BuildTarget.Android => "Builds/Android/CityBuilder.apk",
                BuildTarget.iOS => "Builds/iOS",
                _ => throw new ArgumentOutOfRangeException(nameof(target), target, "No development build is set up for this target (docs/BUILD.md)."),
            };
        }

        /// <summary>Builds the active build target (the Editor's <c>-buildTarget</c> option); exits 0 on success, 1 otherwise.</summary>
        public static void BuildFromCommandLine()
        {
            bool succeeded = Build(EditorUserBuildSettings.activeBuildTarget);
            EditorApplication.Exit(succeeded ? 0 : 1);
        }

        /// <summary>Builds the active build target from the Editor's menu.</summary>
        [MenuItem("CityBuilder/Development build (active target)")]
        public static void BuildFromMenu()
        {
            Build(EditorUserBuildSettings.activeBuildTarget);
        }

        /// <summary>Builds <paramref name="target"/> into its default output and logs the result.</summary>
        /// <param name="target">The platform.</param>
        /// <returns>Whether the build succeeded.</returns>
        private static bool Build(BuildTarget target)
        {
            string output = DefaultOutput(target);
            BuildReport report = BuildPipeline.BuildPlayer(Options(target, output));
            BuildSummary summary = report.summary;
            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"DevBuild: {target} built into {output} ({summary.totalSize / (1024 * 1024)} MB, {summary.totalTime.TotalSeconds:0} s).");
                return true;
            }

            Debug.LogError($"DevBuild: the {target} build {summary.result} with {summary.totalErrors} error(s); see the log above.");
            return false;
        }
    }
}
