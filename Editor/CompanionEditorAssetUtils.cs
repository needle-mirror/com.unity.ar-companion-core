// #define AR_COMPANION_DATA_LOG

using System;
using System.IO;
using Unity.RuntimeSceneSerialization;
using System.Linq;
using Unity.AR.Companion.CloudStorage;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

#if INCLUDE_MARS
using MarsMarkerLibrary = Unity.MARS.Data.MarsMarkerLibrary;
#endif

namespace Unity.AR.Companion.Core
{
    static class CompanionEditorAssetUtils
    {
        const string k_AssetBundlePath = "Temp/CompanionAssetBundles";
        const string k_MarkerLibraryAssetSearch = "t:MarsMarkerLibrary";
        const string k_MarkersPropertyPath = "m_Markers";
        const string k_LabelPropertyPath = "m_Label";
        const string k_TexturePropertyPath = "m_Texture";
        const string k_SizePropertyPath = "m_Size";

        public static void BuildAssetBundles(AssetBundleBuild[] builds)
        {
            var outputPath = GetTempPath();
            if (!Directory.Exists(outputPath))
                Directory.CreateDirectory(outputPath);

            Debug.Log("Exporting AssetBundles");
            var manifest = BuildPipeline.BuildAssetBundles(outputPath, builds, BuildAssetBundleOptions.None, EditorUserBuildSettings.activeBuildTarget);
            if (manifest == null)
                throw new BuildFailedException("Failed to build AssetBundles");
        }

        static string GetTempAssetBundlePath(string name) { return Path.Combine(GetTempPath(), name.ToLowerInvariant()); }

        static string GetTempPath()
        {
            var fullPath = Directory.GetParent(Application.dataPath).FullName;
            fullPath = Path.Combine(fullPath, k_AssetBundlePath);
            return fullPath;
        }

        internal static RequestHandle UploadSceneAssetBundle(this IUsesCloudStorage storageUser, string resourceFolder,
            string platform, string guid, Action<bool, string, long> callback = null, ProgressCallback progress = null)
        {
            return UploadAssetBundle(storageUser, CompanionAssetUtils.SceneAssetBundleGroupName, resourceFolder, platform, guid, callback, progress);
        }

        internal static RequestHandle SavePrefab(this IUsesCloudStorage storageUser, CompanionProject project,
            string resourceFolder, GameObject prefab, Action<bool, string> callback = null)
        {
            // Write to local storage in case cloud isn't reachable
            var prefabName = prefab.name;
            var path = AssetDatabase.GetAssetPath(prefab);
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError("Cannot get path for " + prefab);
                return default;
            }

            var guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid))
            {
                Debug.LogError("Cannot get asset guid for path " + path);
                return default;
            }

            var prefabResource = new Prefab(prefabName);
            var jsonText = SceneSerialization.ToJson(prefabResource);
            CompanionAssetUtils.WriteLocalPrefab(project, resourceFolder, guid, jsonText);

            var prefabKey = CompanionAssetUtils.GetPrefabKey(resourceFolder, guid);
            return storageUser.CloudSaveAsync(prefabKey, jsonText, true,
                (success, responseCode, response) =>
                {
                    Debug.LogFormat(success ? "Prefab resource {0} saved" : "Failed to save prefab resource {0}", prefabName);
                    callback?.Invoke(success, prefabKey);
                });
        }

        internal static RequestHandle UploadPrefabAssetBundle(this IUsesCloudStorage storageUser, string resourceFolder,
            string platform, string guid, Action<bool, string, long> callback = null, ProgressCallback progress = null)
        {
            return UploadAssetBundle(storageUser, CompanionAssetUtils.PrefabAssetBundleGroupName, resourceFolder, platform, guid, callback, progress);
        }

        static RequestHandle UploadAssetBundle(this IUsesCloudStorage storageUser, string group, string resourceFolder,
            string platform, string guid, Action<bool, string, long> callback = null, ProgressCallback progress = null)
        {
            const int timeout = 0; // Asset bundles may be quite large and take a long time to upload
            var bundlePath = GetTempAssetBundlePath(guid);
            if (!File.Exists(bundlePath))
            {
                Debug.LogError("Could not find AssetBundle at path: " + bundlePath);
                callback?.Invoke(false, null, 0);
            }

            var key = CompanionAssetUtils.GetAssetBundleKey(group, resourceFolder, platform, guid);
            return storageUser.CloudSaveFileAsync(key, bundlePath, true,
                (success, responseCode, response) =>
                {
                    Debug.LogFormat(success ? "AssetBundle {0} uploaded" : "Failed to upload AssetBundle {0}", key);
                    var fileSize = 0L;
                    try
                    {
                        var bundleFile = File.OpenRead(bundlePath);
                        fileSize = bundleFile.Length;
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Error getting bundle file size for {bundlePath}");
                        Debug.LogException(e);
                    }

                    callback?.Invoke(success, key, fileSize);
                }, progress, timeout);
        }
#if INCLUDE_MARS
        internal static MarsMarkerLibrary[] LoadAllMarkerLibraryAssets()
        {
            return AssetDatabase.FindAssets(k_MarkerLibraryAssetSearch)
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<MarsMarkerLibrary>).ToArray();
        }

        internal static MarsMarkerLibrary CreateAndSaveNewLibrary()
        {
            var newMarkerLibrary = ScriptableObject.CreateInstance<MarsMarkerLibrary>();

            const string saveDialogTitle = "Save new image marker library";
            const string saveDialogMessage = "Choose where to save the new library";
            const string defaultFile = "New Image Marker Library";

            var savePath = EditorUtility.SaveFilePanelInProject(saveDialogTitle, defaultFile, "asset", saveDialogMessage);
            if (string.IsNullOrEmpty(savePath))
                return null;

            AssetDatabase.CreateAsset(newMarkerLibrary, savePath);
            AssetDatabase.SaveAssets();
            return newMarkerLibrary;
        }

        internal static Guid CreateMarkerInLibrary(MarsMarkerLibrary library, Marker marker, Texture2D markerImage, Vector2 size = default)
        {
            var serializedObject = new SerializedObject(library);
            var markersProperty = serializedObject.FindProperty(k_MarkersPropertyPath);
            var arraySize = markersProperty.arraySize;
            markersProperty.arraySize = arraySize + 1;
            var element = markersProperty.GetArrayElementAtIndex(arraySize);
            element.FindPropertyRelative(k_LabelPropertyPath).stringValue = marker.name;
            element.FindPropertyRelative(k_TexturePropertyPath).objectReferenceValue = markerImage;
            if (size != default)
                element.FindPropertyRelative(k_SizePropertyPath).vector2Value = size;

            serializedObject.ApplyModifiedProperties();
            library.SaveMarkerLibrary();

            var guid = Guid.NewGuid();
            library.SetGuid(arraySize, guid);

#if AR_COMPANION_DATA_LOG
            Debug.Log($"Added new marker '{marker.name}' to image marker library '{library.name}'");
#endif
            return guid;
        }
#endif
    }
}
