// #define AR_COMPANION_DATA_LOG

using System;
using System.Collections.Generic;
using System.IO;
using Unity.AR.Companion.Analytics;
using Unity.XRTools.ModuleLoader;
using Unity.RuntimeSceneSerialization;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.AR.Companion.CloudStorage;

#if INCLUDE_MARS
using Unity.MARS;
#endif

namespace Unity.AR.Companion.Core
{
    static class CompanionSceneUtils
    {
        const string k_SceneGroupName = "Scenes";
        const string k_UntitledSceneName = "Untitled Scene";
        const string k_FileFormat = "{0}.json";

        // Local method use only -- created here to reduce garbage collection. Collections must be cleared before use
        static readonly List<IFunctionalitySubscriber> k_Subscribers = new List<IFunctionalitySubscriber>();
        static readonly List<object> k_Objects = new List<object>();
        static readonly HashSet<Type> k_SubscriberTypes = new HashSet<Type>();

        internal static string GetSceneName(string name)
        {
            if (string.IsNullOrEmpty(name))
                name = k_UntitledSceneName;

            return name;
        }

        internal static string GetSceneKey(string resourceFolder, string guid)
        {
            return $"{k_SceneGroupName}_{resourceFolder}_{guid}";
        }

        public static void SplitSceneKey(string key, out string resourceFolder, out string guid)
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

        internal static RequestHandle GetScene(this IUsesCloudStorage storageUser, CompanionProject project,
            CompanionResource resource, Action<bool, string> callback, ProgressCallback progress = null)
        {
            if (callback == null)
            {
                Debug.LogWarning("Callback is null in GetScene");
                return default;
            }

            var key = resource.key;
            if (!project.linked)
            {
                GetLocalScene(project, key, true, callback);
                return default;
            }

            ResourceSyncEvent.SendEvent(SyncAction.Download, resource.key, resource.type, resource.fileSize);

            SplitSceneKey(key, out var resourceFolder, out var sceneGuid);

            // Do not try to get cloud resource unless it came from cloud resource list
            // This is in case a cloud version of this scene was uploaded between fetching the resource list and opening a scene
            var isCloud = resource.isCloudResource;
            var duplicate = resource.cloudVersion;
            if (duplicate != null && duplicate.isCloudResource)
                isCloud = true;

            if (isCloud)
            {
                var showIssueDialog = duplicate == null;
                return storageUser.CloudLoadAsync(key, showIssueDialog,
                    (cloudSuccess, responseCode, response) =>
                    {
                        if (cloudSuccess)
                        {
                            WriteLocalScene(project, resourceFolder, sceneGuid, response);
                            CompanionResourceUtils.AddCloudResourceToLocalResourceList(project, resourceFolder, resource);
                            callback(true, response);
                        }
                        else
                        {
                            if (resource.GetSyncStatus() == SynchronizationStatus.CloudOnly)
                            {
                                callback(false, response);
                                if (storageUser.HasValidIdentity())
                                    CompanionIssueUtils.HandleIssue(CoreIssueCodes.CompanionSceneDownloadFailed);

                                return;
                            }

                            GetLocalScene(project, key, true, callback);
                        }
                    }, progress);
            }

            GetLocalScene(project, key, true, callback);
            return default;
        }

        static void GetLocalScene(CompanionProject project, string key, bool showIssueDialog, Action<bool, string> callback)
        {
            var jsonText = GetLocalScene(project, key, showIssueDialog);
            callback(!string.IsNullOrEmpty(jsonText), jsonText);
        }

        static string GetLocalScene(CompanionProject project, string key, bool showIssueDialog)
        {
            SplitSceneKey(key, out var resourceFolder, out var guid);
            var folder = CompanionResourceUtils.GetLocalResourceFolderPath(project, resourceFolder, k_SceneGroupName);
            var filename = string.Format(k_FileFormat, guid);
            var path = Path.Combine(folder, filename);

#if AR_COMPANION_DATA_LOG
            Debug.Log("Get scene from path " + path);
#endif
            try
            {
                if (!File.Exists(path))
                {
                    Debug.LogWarningFormat("Could not find scene with key {0}", key);
                    if(showIssueDialog)
                        CompanionIssueUtils.HandleIssue(CoreIssueCodes.CompanionFileMissing);

                    return null;
                }

                return File.ReadAllText(path);
            }
            catch (Exception e)
            {
                if (showIssueDialog)
                    CompanionIssueUtils.HandleIssue(CoreIssueCodes.CompanionFileRead, e);

                return null;
            }
        }

        internal static RequestHandle SaveScene(this IUsesCloudStorage storageUser, CompanionProject project,
            string resourceFolder, string guid, Scene scene, SerializedRenderSettings renderSettings,
            AssetPack assetPack, Action<bool, string, long> callback)
        {
            var jsonText = SceneSerialization.SerializeScene(scene, renderSettings, assetPack);

#if UNITY_EDITOR
            if (!Application.isPlaying && assetPack != null && assetPack.AssetCount > 0)
            {
                if (!AssetDatabase.Contains(assetPack))
                {
                    var sceneName = scene.name;
                    var assetPackPath = EditorUtility.SaveFilePanelInProject("Save Asset Pack", sceneName, "asset", "Save Asset Pack", Path.GetDirectoryName(scene.path));
                    if (string.IsNullOrEmpty(assetPackPath))
                    {
                        Debug.LogWarning($"Canceled publish of {sceneName}. You must save an asset pack in order to publish a scene with asset references.");
                        callback?.Invoke(false, null, 0);
                        return default;
                    }

                    AssetDatabase.CreateAsset(assetPack, assetPackPath);
                }

                Debug.Log("Published scene has asset references. Use the Companion Resource window to export asset bundles", assetPack);
                EditorUtility.SetDirty(assetPack);
            }
#endif

            var key = GetSceneKey(resourceFolder, guid);
            if (Application.isPlaying)
            {
                // Write to local storage in case cloud isn't reachable
                WriteLocalScene(project, resourceFolder, guid, jsonText);
                if (!project.linked)
                {
                    callback?.Invoke(true, key, jsonText.Length);
                    return default;
                }
            }

            return storageUser.CloudSaveAsync(key, jsonText, true, (success, responseCode, response) =>
            {
#if AR_COMPANION_DATA_LOG
                Debug.LogFormat(success ? "Scene {0} saved" : "Failed to save scene {0}", sceneName);
#endif

                callback?.Invoke(success, key, jsonText.Length);
                if (!success && storageUser.HasValidIdentity())
                    CompanionIssueUtils.HandleIssue(CoreIssueCodes.CompanionUploadFailed);
            });
        }

        public static void CopySceneResource(this IUsesCloudStorage storageUser, CompanionProject project,
            CompanionResource resource, Stack<RequestHandle> requests, Action<bool> callback) //Need to send back Key so ResourceList
        {
            var key = resource.key;
            var jsonText = GetLocalScene(project, key, true);
            if (string.IsNullOrEmpty(jsonText))
            {
                callback(false);
                return;
            }

            var newGuid = Guid.NewGuid().ToString();
            SplitSceneKey(key, out var resourceFolder, out _);
            var newKey = GetSceneKey(resourceFolder, newGuid);
            WriteLocalScene(project, resourceFolder, newGuid, jsonText);

            void GetAndSaveThumbnail()
            {
                resource.GetThumbnail(storageUser, project, requests, thumbnail =>
                {
                    if (thumbnail != null)
                    {
                        storageUser.SaveThumbnailImage(project, resourceFolder, newKey, thumbnail.EncodeToPNG(), true, requests,
                            (thumbnailSuccess, thumbnailKey) =>
                            {
                                if (!thumbnailSuccess)
                                {
                                    callback?.Invoke(false);
                                    CompanionIssueUtils.HandleIssue(CoreIssueCodes.CompanionUploadFailed);
                                    return;
                                }

                                storageUser.AddOrUpdateResource(project, resourceFolder, newKey, $"{resource.name} Copy",
                                    ResourceType.Scene, jsonText.Length, resource.hasAssetBundle, true, requests,
                                    (resourceListSuccess, resourceList) =>
                                    {
                                        callback?.Invoke(resourceListSuccess);
                                    });
                            });
                    }
                    else
                    {
                        storageUser.AddOrUpdateResource(project, resourceFolder, newKey, $"{resource.name} Copy",
                            ResourceType.Scene, jsonText.Length, resource.hasAssetBundle, true, requests,
                            (resourceListSuccess, resourceList) =>
                            {
                                callback?.Invoke(resourceListSuccess);
                            });
                    }
                });
            }

            if (project.linked)
            {
                requests.Push(storageUser.CloudSaveAsync(newKey, jsonText, true,
                    (cloudSaveSuccess, responseCode, response) =>
                    {
                        if (cloudSaveSuccess)
                        {
                            GetAndSaveThumbnail();
                        }
                        else
                        {
                            callback?.Invoke(false);
                            CompanionIssueUtils.HandleIssue(CoreIssueCodes.CompanionUploadFailed);
                        }
                    }));
            }
            else
            {
                GetAndSaveThumbnail();
            }
        }

#if UNITY_EDITOR
        internal static AssetPack GetOrTryCreateAssetPackForSceneAsset(SceneAsset sceneAsset)
        {
            var assetPack = AssetPack.GetAssetPackForScene(sceneAsset);

            if (!assetPack)
                assetPack = ScriptableObject.CreateInstance<AssetPack>();

            return assetPack;
        }
#endif

        static void WriteLocalScene(CompanionProject project, string resourceFolder, string guid, string jsonText)
        {
            var folder = CompanionResourceUtils.GetLocalResourceFolderPath(project, resourceFolder, k_SceneGroupName);
            var filename = string.Format(k_FileFormat, guid);
            var path = Path.Combine(folder, filename);

#if AR_COMPANION_DATA_LOG
            Debug.Log("Write scene to path " + path);
#endif

            if (File.Exists(path))
                File.Delete(path);

            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            CoroutineUtils.StartCoroutine(CompanionFileUtils.WriteFileAsync(path, jsonText));
        }

        public static void DeleteLocalScene(CompanionProject project, string sceneKey)
        {
            SplitSceneKey(sceneKey, out var resourceFolder, out var guid);
            var folder = CompanionResourceUtils.GetLocalResourceFolderPath(project, resourceFolder, k_SceneGroupName);
            var filename = string.Format(k_FileFormat, guid);
            var path = Path.Combine(folder, filename);

#if AR_COMPANION_DATA_LOG
            Debug.Log("Delete scene at path " + path);
#endif

            if (File.Exists(path))
                File.Delete(path);
        }

        internal static void SaveSceneAndUpdateResourceList(this IUsesCloudStorage storageUser, CompanionProject project,
            string key, Scene scene, SerializedRenderSettings renderSettings, AssetPack assetPack, Stack<RequestHandle> requests,
            byte[] thumbnail = null, Action<bool, bool, string, ResourceList> callback = null)
        {
            if (string.IsNullOrEmpty(key))
            {
                Debug.LogError("Cannot save scene with null key");
                return;
            }

            SplitSceneKey(key, out var resourceFolder, out var guid);
            Debug.Log(assetPack != null
                ? $"Saving {scene.name} with guid {guid} and {assetPack}"
                : $"Saving {scene.name} with guid {guid} and no asset pack");

            storageUser.SaveSceneAndUpdateResourceList(project, resourceFolder, guid, scene, renderSettings, assetPack, requests, thumbnail, callback);
        }

        static void SaveSceneAndUpdateResourceList(this IUsesCloudStorage storageUser, CompanionProject project,
            string resourceFolder, string guid, Scene scene, SerializedRenderSettings renderSettings, AssetPack assetPack,
            Stack<RequestHandle> requests, byte[] thumbnail = null, Action<bool, bool, string, ResourceList> callback = null)
        {
            // Capture scene name here because it will be unloaded by the time we get into the callback
            var sceneName = scene.name;
            SaveScene(storageUser, project, resourceFolder, guid, scene, renderSettings, assetPack,
                (sceneSuccess, key, fileSize) =>
                {
                    var hasBundle = assetPack != null && assetPack.AssetCount > 0;
                    if (thumbnail != null)
                    {
                        storageUser.SaveThumbnailImage(project, resourceFolder, key, thumbnail, sceneSuccess, requests,
                            (thumbnailSuccess, thumbnailKey) =>
                            {
                                storageUser.AddOrUpdateResource(project, resourceFolder, key, sceneName, ResourceType.Scene,
                                    fileSize, hasBundle, thumbnailSuccess, requests, (writeListSuccess, resourceList) =>
                                    {
                                        callback?.Invoke(true, writeListSuccess, key, resourceList);
                                    });
                            });
                    }
                    else
                    {
                        storageUser.AddOrUpdateResource(project, resourceFolder, key, sceneName, ResourceType.Scene,
                            fileSize, hasBundle, sceneSuccess, requests, (writeListSuccess, resourceList) =>
                            {
                                callback?.Invoke(true, writeListSuccess, key, resourceList);
                            });
                    }
                });
        }

        internal static SerializationMetadata ImportScene(string jsonText, AssetPack assetPack = null, FunctionalityIsland island = null)
        {
            return SceneSerialization.ImportScene(jsonText, assetPack, roots =>
            {
                k_Objects.Clear();
                foreach (var root in roots)
                {
                    root.GetComponentsInChildren(true, k_Subscribers);
                    k_Objects.AddRange(k_Subscribers);
                    foreach (var subscriber in k_Subscribers)
                    {
                        k_SubscriberTypes.Add(subscriber.GetType());
                    }
                }

                if (island != null)
                {
                    island.SetupDefaultProviders(k_SubscriberTypes);
                    island.InjectFunctionality(k_Objects);
                }

#if INCLUDE_MARS
                foreach (var root in roots)
                {
                    // Find the session, if there is one
                    var session = root.GetComponentInChildren<MARSSession>(true);
                    if (session != null)
                    {
                        var sessionObject = session.gameObject;
                        if (Application.isPlaying)
                        {
                            // In play mode/runtime, deactivate to prevent interfering with the current session
                            if (sessionObject.activeSelf)
                                sessionObject.SetActive(false);
                        }
                        else
                        {
                            // In edit mode, activate to ensure simulation works
                            if (!sessionObject.activeSelf)
                                sessionObject.SetActive(true);
                        }
                    }
                }
#endif
            });
        }

        internal static RequestHandle UploadScene(this IUsesCloudStorage storageUser, CompanionProject project,
            CompanionResource resource, Action<bool> callback = null)
        {
            if (!project.linked)
            {
                callback?.Invoke(false);
                return default;
            }

            var key = resource.key;
            var jsonText = GetLocalScene(project, key, true);
            if (string.IsNullOrEmpty(jsonText))
            {
                callback?.Invoke(false);
                return default;
            }

            return storageUser.CloudSaveAsync(key, jsonText, true,
                (success, responseCode, response) =>
                {
                    callback?.Invoke(success);
                    if (!success)
                        CompanionIssueUtils.HandleIssue(CoreIssueCodes.CompanionUploadFailed);
                });
        }

        internal static RequestHandle DownloadScene(this IUsesCloudStorage storageUser, CompanionProject project,
            CompanionResource resource, Action<bool> callback = null)
        {
            if (!project.linked)
            {
                callback?.Invoke(false);
                return default;
            }

            return storageUser.GetScene(project, resource, (success, jsonText) =>
            {
                if (!success || string.IsNullOrEmpty(jsonText))
                {
                    callback?.Invoke(false);
                    CompanionIssueUtils.HandleIssue(CoreIssueCodes.CompanionSceneDownloadFailed);
                    return;
                }

                SplitSceneKey(resource.key, out var resourceFolder, out var guid);
                WriteLocalScene(project, resourceFolder, guid, jsonText);
                callback?.Invoke(true);
            });
        }
    }
}
