// #define AR_COMPANION_DATA_LOG

using System;
using System.IO;
using Unity.AR.Companion.CloudStorage;
using Unity.RuntimeSceneSerialization;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Unity.AR.Companion.Core
{
    /// <summary>
    /// Utility methods for synchronizing assets using cloud storage
    /// </summary>
    static class CompanionAssetUtils
    {
        internal const string SceneAssetBundleGroupName = "SceneAssetBundles";
        internal const string PrefabAssetBundleGroupName = "PrefabAssetBundles";

        const string k_PrefabGroupName = "Prefabs";
        const string k_FileFormat = "{0}.json";

        internal static string GetAssetBundleKey(string group, string resourceFolder, string platform, string guid)
        {
            return $"{group}_{resourceFolder}_{platform}_{guid}";
        }

        internal static string GetPrefabKey(string resourceFolder, string guid) { return $"{k_PrefabGroupName}_{resourceFolder}_{guid}"; }

        internal static void SplitPrefabKey(string key, out string resourceFolder, out string guid)
        {
            var parts = key.Split('_');
            if (parts.Length != 3)
            {
                resourceFolder = null;
                guid = null;
                return;
            }

            resourceFolder = parts[1];
            guid = parts[2];
        }

        static void SplitAssetKey(string key, out string resourceFolder, out string platform, out string guid)
        {
            var parts = key.Split('_');
            if (parts.Length != 4)
            {
                resourceFolder = null;
                platform = null;
                guid = null;
                return;
            }
            resourceFolder = parts[1];
            platform = parts[2];
            guid = parts[3];
        }

        static RequestHandle GetAssetBundleAsset(this IUsesCloudStorage storageUser, string group, CompanionProject project,
            string resourceFolder, string platform, string guid, Action<bool, UnityObject> callback, ProgressCallback progress = null)
        {
            const int timeout = 0; // Asset bundles may be quite large and take a long time to download

            // Write to local storage in case cloud isn't reachable
            var folder = CompanionResourceUtils.GetLocalResourceFolderPath(project, resourceFolder, group);
            var path = Path.Combine(folder, guid);
            if (!project.linked)
            {
                UnityObject asset = null;
                if (File.Exists(path))
                    asset = GetLocalAssetBundleAsset(path);

                var hasAsset = asset != null;
                callback(hasAsset, asset);
                if (!hasAsset)
                {
                    CompanionIssueUtils.HandleIssue(CoreIssueCodes.CompanionAssetLoadError, "This will cause dependent scenes and bundles to have missing references.");
                }

                return default;
            }

            // TODO: Store a version number and check local/cached version--skip download if local version == cloud version
            var key = GetAssetBundleKey(group, resourceFolder, platform, guid);
            return storageUser.CloudLoadAsync(key, false,
                (bool success, long responseCode, byte[] response) =>
                {
                    void OnFileWriteComplete(bool writeSuccess, string writtenPath)
                    {
                        UnityObject asset = null;
                        if (File.Exists(writtenPath))
                            asset = GetLocalAssetBundleAsset(writtenPath);

                        callback(success, asset);
                    }

                    if (success)
                    {
                        if (File.Exists(path))
                            File.Delete(path);

                        if (!Directory.Exists(folder))
                            Directory.CreateDirectory(folder);

                        CoroutineUtils.StartCoroutine(CompanionFileUtils.WriteFileAsync(path, response, OnFileWriteComplete));
                    }
                    else
                    {
                        Debug.LogWarning("Failed to download asset bundle. Falling back to cached version if it exists...");
                        OnFileWriteComplete(true, path);
                    }
                }, progress, timeout);
        }

        static UnityObject GetLocalAssetBundleAsset(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogError($"AssetBundle failed to load from {path}. File does not exist");
                CompanionIssueUtils.HandleIssue(CoreIssueCodes.CompanionFileMissing);
                return null;
            }

            var assetBundle = AssetBundle.LoadFromFile(path);
            if (assetBundle == null)
            {
                var fileInfo = new FileInfo(path);
                var readableSize = CompanionFileUtils.GetReadableFileSize(fileInfo.Length);
                Debug.LogError($"AssetBundle failed to load from {path}. File exists and is {readableSize}");
                CompanionIssueUtils.HandleIssue(CoreIssueCodes.CompanionAssetLoadError);

                return null;
            }

            var assets = assetBundle.LoadAllAssets();
            UnityObject asset = null;
            if (assets.Length > 0)
                asset = assets[0];

            assetBundle.Unload(false);
            return asset;
        }

        static RequestHandle GetAssetPack(this IUsesCloudStorage storageUser, CompanionProject project,
            string resourceFolder, string platform, string guid, Action<bool, AssetPack> callback)
        {
            return storageUser.GetAssetBundleAsset(SceneAssetBundleGroupName, project, resourceFolder, platform, guid, (success, asset) =>
            {
                if (asset == null)
                    callback(success, null);
                else
                    callback(success, (AssetPack)asset);
            });
        }

        internal static void GetPrefabAsset(this IUsesCloudStorage storageUser, CompanionProject project, string key, Action<bool, GameObject> callback)
        {
#if UNITY_EDITOR
            var platform = GetCurrentBuildTargetAsRuntimePlatform();
#else
            var platform = Application.platform;
#endif
            storageUser.GetPrefabAsset(project, key, platform.ToString(), callback);
        }

        internal static RequestHandle GetPrefabAsset(this IUsesCloudStorage storageUser, CompanionProject project, string key,
            string platform, Action<bool, GameObject> callback, ProgressCallback progress = null)
        {
            SplitPrefabKey(key, out var resourceFolder, out var guid);

            return storageUser.GetAssetBundleAsset(PrefabAssetBundleGroupName, project, resourceFolder, platform, guid, (success, asset) =>
            {
                if (asset == null)
                    callback(success, null);
                else
                    callback(success, (GameObject)asset);
            }, progress);
        }

        internal static RequestHandle GetAssetPack(this IUsesCloudStorage storageUser, CompanionProject project, string key, Action<bool, AssetPack> callback)
        {
            SplitAssetKey(key, out var resourceFolder, out var platform, out var guid);
            return storageUser.GetAssetPack(project, resourceFolder, platform, guid, callback);
        }

        static string GetLocalPrefab(CompanionProject project, string key)
        {
            SplitPrefabKey(key, out var resourceFolder, out var guid);
            var folder = CompanionResourceUtils.GetLocalResourceFolderPath(project, resourceFolder, k_PrefabGroupName);
            var filename = string.Format(k_FileFormat, guid);
            var path = Path.Combine(folder, filename);

#if AR_COMPANION_DATA_LOG
            Debug.Log("Get prefab from path " + path);
#endif

            if (!File.Exists(path))
            {
                Debug.LogWarningFormat("Could not find prefab with key {0}", key);
                CompanionIssueUtils.HandleIssue(CoreIssueCodes.CompanionFileMissing);
                return null;
            }

            return File.ReadAllText(path);
        }

        internal static void WriteLocalPrefab(CompanionProject project, string resourceFolder, string guid, string jsonText)
        {
            // Write to local storage in case cloud isn't reachable
            var folder = CompanionResourceUtils.GetLocalResourceFolderPath(project, resourceFolder, k_PrefabGroupName);
            var filename = string.Format(k_FileFormat, guid);
            var path = Path.Combine(folder, filename);

#if AR_COMPANION_DATA_LOG
            Debug.Log("Write prefab to path " + path);
#endif

            CoroutineUtils.StartCoroutine(CompanionFileUtils.WriteFileAsync(path, jsonText));
        }

        internal static void DeleteLocalPrefab(CompanionProject project, string key)
        {
            SplitPrefabKey(key, out var resourceFolder, out var guid);
            var folder = CompanionResourceUtils.GetLocalResourceFolderPath(project, resourceFolder, k_PrefabGroupName);
            var filename = string.Format(k_FileFormat, guid);
            var path = Path.Combine(folder, filename);

#if AR_COMPANION_DATA_LOG
            Debug.Log("Delete scene at path " + path);
#endif

            if (File.Exists(path))
                File.Delete(path);
        }

        internal static RequestHandle UploadPrefab(this IUsesCloudStorage storageUser, CompanionProject project, CompanionResource resource, Action<bool> callback = null)
        {
            if (!project.linked)
            {
                Debug.LogError("Cannot upload prefab in unlinked project");
                callback?.Invoke(false);
                return default;
            }

            var key = resource.key;
            var jsonText = GetLocalPrefab(project, key);
            if (string.IsNullOrEmpty(jsonText))
            {
                callback?.Invoke(false);
                return default;
            }

            // TODO: upload downloaded asset bundles (if they are missing from the cloud)
            return storageUser.CloudSaveAsync(key, jsonText, true,
                (success, responseCode, response) =>
                {
                    callback?.Invoke(success);
                    if (!success)
                        CompanionIssueUtils.HandleIssue(CoreIssueCodes.CompanionUploadFailed);
                });
        }

        internal static RequestHandle DownloadPrefab(this IUsesCloudStorage storageUser, CompanionProject project,
            CompanionResource resource, Action<bool> callback = null)
        {
            if (!project.linked)
            {
                Debug.LogError("Cannot download prefab in unlinked project");
                callback?.Invoke(false);
                return default;
            }

            return storageUser.CloudLoadAsync(resource.key, true,
                (success, responseCode, response) =>
                {
                    if (!success || string.IsNullOrEmpty(response))
                    {
                        callback?.Invoke(false);
                        CompanionIssueUtils.HandleIssue(CoreIssueCodes.CompanionDownloadFailed);
                        return;
                    }

                    SplitPrefabKey(resource.key, out var resourceFolder, out var guid);
                    WriteLocalPrefab(project, resourceFolder, guid, response);
                    callback?.Invoke(true);
                });
        }

#if UNITY_EDITOR
        /// <summary>
        /// Return a RuntimePlatform equivalent to the current build target
        /// </summary>
        /// <returns>The RuntimePlatform equivalent of the current build target</returns>
        public static RuntimePlatform GetCurrentBuildTargetAsRuntimePlatform()
        {
            var buildTarget = EditorUserBuildSettings.activeBuildTarget;
            switch (buildTarget)
            {
                case BuildTarget.Android:
                    return RuntimePlatform.Android;
                case BuildTarget.iOS:
                    return RuntimePlatform.IPhonePlayer;
                case BuildTarget.Lumin:
                    return RuntimePlatform.Lumin;
                case BuildTarget.StandaloneOSX:
                    return RuntimePlatform.OSXPlayer;
                case BuildTarget.StandaloneWindows:
                    return RuntimePlatform.WindowsPlayer;
                case BuildTarget.StandaloneWindows64:
                    return RuntimePlatform.WindowsPlayer;
                case BuildTarget.WebGL:
                    return RuntimePlatform.WebGLPlayer;
                // TODO: More info needed
                //case BuildTarget.WSAPlayer:
                //    return RuntimePlatform.WSAPlayerARM;
                case BuildTarget.StandaloneLinux64:
                    return RuntimePlatform.LinuxPlayer;
                case BuildTarget.PS4:
                    return RuntimePlatform.PS4;
                case BuildTarget.XboxOne:
                    return RuntimePlatform.XboxOne;
                case BuildTarget.tvOS:
                    return RuntimePlatform.tvOS;
                case BuildTarget.Switch:
                    return RuntimePlatform.Switch;
                case BuildTarget.Stadia:
                    return RuntimePlatform.Stadia;
                default:
                    throw new ArgumentException($"Build target {buildTarget} not supported.");
            }
        }
#endif
    }
}
