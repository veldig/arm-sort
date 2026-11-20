using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ArmSort.EditorTools
{
    // Arm Sort > Build Web: builds the game for browsers into Builds/Web.
    // Gzip with decompression fallback, because GitHub Pages does not send the
    // Content-Encoding header Unity's compressed files expect.
    static class WebBuild
    {
        const string Output = "Builds/Web";

        [MenuItem("Arm Sort/Build Web")]
        static void Build()
        {
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;

            var scenes = new string[EditorBuildSettings.scenes.Length];
            for (int i = 0; i < scenes.Length; i++) scenes[i] = EditorBuildSettings.scenes[i].path;

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Output,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            });

            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"Arm Sort web build done: {Output} ({report.summary.totalSize / 1024 / 1024} MB)");
                EditorUtility.RevealInFinder(Output);
            }
            else
            {
                Debug.LogError($"Arm Sort web build failed: {report.summary.result}");
            }
        }
    }
}
