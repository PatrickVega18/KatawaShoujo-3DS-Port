using UnityEditor;
using System.IO;

public class CreateAssetBundles
{
    [MenuItem("Assets/Build AssetBundles (LZ4)")]
    static void BuildAllAssetBundles()
    {
        string assetBundleDirectory = "Assets/StreamingAssets";
        if (!Directory.Exists(assetBundleDirectory))
        {
            Directory.CreateDirectory(assetBundleDirectory);
        }
        BuildTarget target = EditorUserBuildSettings.activeBuildTarget;

        BuildPipeline.BuildAssetBundles(assetBundleDirectory, 
            BuildAssetBundleOptions.ChunkBasedCompression, 
            target);
            
        UnityEngine.Debug.Log("AssetBundles LZ4 creados para: " + target.ToString());
    }
}