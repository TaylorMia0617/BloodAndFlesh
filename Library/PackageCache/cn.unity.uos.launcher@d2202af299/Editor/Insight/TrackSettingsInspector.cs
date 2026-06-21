using System.IO;
using Unity.UOS.Insight.Config;
using UnityEditor;
using UnityEngine;

namespace Unity.UOS.Insight.Editor
{
    public static class TrackSettingsInspector
    {
        private const string Dir = "Assets/Resources";
        private const string Path = Dir + "/TrackSettings.asset";

        [MenuItem("UOS/Insight/Track Settings")]
        public static void CreateOrSelect()
        {
            var asset = LoadOrCreate();
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        private static TrackSettings LoadOrCreate()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TrackSettings>(Path);
            if (!asset)
            {
                Directory.CreateDirectory(Dir);
                asset = ScriptableObject.CreateInstance<TrackSettings>();
                AssetDatabase.CreateAsset(asset, Path);
                AssetDatabase.SaveAssets();
            }

            return asset;
        }
    }
}