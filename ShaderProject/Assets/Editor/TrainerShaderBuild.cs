using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class TrainerShaderBuild
{
    public static string Output => Path.GetFullPath(Path.Combine(Application.dataPath, "../../artifacts/shaders"));

    public static void Build()
    {
        try
        {
            Directory.CreateDirectory(Output);
            PlayerSettings.colorSpace = ColorSpace.Gamma;
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { UnityEngine.Rendering.GraphicsDeviceType.Direct3D11 });
            var paths = new[] { "Assets/Trainer/HighlightMask.shader", "Assets/Trainer/HighlightComposite.shader" };
            foreach (var path in paths)
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                if (shader == null) throw new Exception("Shader missing: " + path);
                foreach (var message in ShaderUtil.GetShaderMessages(shader))
                    if (message.severity.ToString() == "Error") throw new Exception(path + ": " + message.message);
            }
            var manifest = BuildPipeline.BuildAssetBundles(Output, new[]
            {
                new AssetBundleBuild { assetBundleName = "trainer-highlights", assetNames = paths }
            }, BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.ForceRebuildAssetBundle, BuildTarget.StandaloneWindows64);
            if (manifest == null) throw new Exception("AssetBundle build failed.");
            if (Environment.GetCommandLineArgs().Contains("-trainerGraphicsChecks")) TrainerGraphicsChecks.Run();
            Debug.Log("TRAINER_SHADER_BUILD_OK");
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }
}
