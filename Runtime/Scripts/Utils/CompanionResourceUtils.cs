// #define AR_COMPANION_DATA_LOG

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.AR.Companion.Analytics;
using Unity.AR.Companion.CloudStorage;
using Unity.RuntimeSceneSerialization;
using UnityEngine;

namespace Unity.AR.Companion.Core
{
    static class CompanionResourceUtils
    {
        public const string ResourceFolderPrefsKey = "AR.Companion.ResourceFolder";
        public const int ThumbnailSize = 512;

        public const string InvalidResourceFolderNameErrorString = "Invalid resource folder name";
        public const string ThumbnailResourceSubFolder = "Thumbnails";

        // Leave room for the longest key type, which is an iphone prefab asset bundle
        const string k_LongestResourceFormatString = "{0}__{1}_af864a3e-2977-4de5-ab92-ae603e019391";
        public static readonly int MaxResourceFolderNameBytes = GenesisCloudStorageModule.MaxKeyLengthBytes -
            Encoding.UTF8.GetByteCount(string.Format(k_LongestResourceFormatString,
            CompanionAssetUtils.SceneAssetBundleGroupName, // Longest resource type name
            RuntimePlatform.IPhonePlayer)); // Longest build platform name

        public static readonly string ResourceFolderNameTooLongErrorString = $"Resource folder names cannot be longer than {MaxResourceFolderNameBytes} bytes. Please choose a shorter resource folder name";

        const string k_ThumbnailKeySuffix = "Thumbnail";

        const string k_LocalResourceListFilename = "ResourceList.json";
        const string k_FallbackCloudResourceListFilename = "CloudResourceList.json";
        const string k_ResourceListGroupName = "ResourceLists";

        const string k_DefaultResourceFolder = "Guest";

        const string k_ImageFileFormat = "{0}.png";

#if UNITY_EDITOR
        public static event Action<ResourceList> CloudResourceListChanged;
#endif

        public static event Action<bool, ResourceList> ResourceListChanged;

        static ResourceList s_FallbackCloudResourceList;

        internal static void UnsetFallbackCloudResourceList()
        {
            s_FallbackCloudResourceList = null;
        }

        static void UnknownIssueHandler(string issueCode, string operation, ResourceType type)
        {
            CompanionIssueUtils.HandleIssue(issueCode, new ArgumentOutOfRangeException($"No {operation} handler for {type.ToString()} resource!"));
        }

        internal static string GetResourceFolder(string name)
        {
            if (string.IsNullOrEmpty(name))
                name = k_DefaultResourceFolder;

            return name;
        }

#if UNITY_EDITOR
        internal static void OnCloudResourceListChanged(ResourceList resourceList)
        {
            CloudResourceListChanged?.Invoke(resourceList);
        }
#endif

        internal static string GetCurrentResourceFolder() { return GetResourceFolder(PlayerPrefs.GetString(ResourceFolderPrefsKey)); }

        static string GetResourceListKey(string projectId, string resourceFolder) { return $"{k_ResourceListGroupName}_{projectId}_{resourceFolder}"; }

        static void SplitResourceKey(string key, out string group, out string resourceFolder, out string guid)
        {
            var parts = key.Split('_');
            if (parts.Length != 3)
            {
                group = null;
                resourceFolder = null;
                guid = null;
                return;
            }

            group = parts[0];
            resourceFolder = parts[1];
            guid = parts[2];
        }

        internal static void AddOrUpdateResource(this IUsesCloudStorage storageUser, CompanionProject project,
            string resourceFolder, string key, string name, ResourceType type, long fileSize, bool hasBundle,
            bool attemptCloudWrite, Stack<RequestHandle> requests, Action<bool, ResourceList> callback = null)
        {
            var localResourceList = GetLocalResourceList(project, resourceFolder) ?? new ResourceList();
            var resource = localResourceList.AddOrUpdateResource(key, name, type, fileSize, hasBundle);

            WriteLocalResourceList(project, resourceFolder, localResourceList);

            void LocalOnlyUpdate(bool success, ResourceList resourceList)
            {
                resourceList = ResourceList.MergeResourceLists(localResourceList, resourceList);
                ResourceListChanged?.Invoke(false, resourceList);
                callback?.Invoke(success, resourceList);
            }

            if (!project.linked)
            {
                LocalOnlyUpdate(true, new ResourceList());
                return;
            }

            if (attemptCloudWrite)
            {
                // Get the latest list to make sure we don't lose scenes in case of stale data
                requests.Push(storageUser.GetCloudResourceList(project, resourceFolder, true,
                    (getListSuccess, cloudResourceList) =>
                    {
                        if (getListSuccess)
                        {
                            cloudResourceList.AddOrOverwriteExistingResource(resource);
                            requests.Push(storageUser.WriteCloudResourceList(project, resourceFolder, cloudResourceList, writeListSuccess =>
                            {
                                if (writeListSuccess)
                                {
#if UNITY_EDITOR
                                    CloudResourceListChanged?.Invoke(cloudResourceList);
#endif

                                    WriteFallbackCloudResourceList(project, resourceFolder, cloudResourceList);
                                    cloudResourceList = ResourceList.MergeResourceLists(localResourceList, cloudResourceList);
                                    ResourceListChanged?.Invoke(true, cloudResourceList);
                                    callback?.Invoke(true, cloudResourceList);
                                }
                                else
                                {
                                    LocalOnlyUpdate(false, cloudResourceList);
                                }
                            }));
                        }
                        else
                        {
                            LocalOnlyUpdate(false, cloudResourceList);
                        }
                    }));
            }
            else
            {
                if (s_FallbackCloudResourceList == null)
                    s_FallbackCloudResourceList = GetFallbackCloudResourceList(project, resourceFolder);

                if (s_FallbackCloudResourceList == null)
                    s_FallbackCloudResourceList = new ResourceList();

                LocalOnlyUpdate(false, s_FallbackCloudResourceList);
            }
        }

        public static void AddCloudResourceToLocalResourceList(CompanionProject project, string resourceFolder, CompanionResource resource)
        {
            var localResourceList = GetLocalResourceList(project, resourceFolder) ?? new ResourceList();
            localResourceList.AddOrOverwriteExistingResource(resource);
            WriteLocalResourceList(project, resourceFolder, localResourceList);
        }

        internal static RequestHandle GetCloudResourceList(this IUsesCloudStorage storageUser, CompanionProject project,
            string resourceFolder, bool showIssueDialog, Action<bool, ResourceList> callback)
        {
            if (callback == null)
            {
                Debug.LogWarning("Callback is null in GetCloudResourceList");
                return default;
            }

            return storageUser.CloudLoadAsync(GetResourceListKey(project.index, resourceFolder), showIssueDialog,
                (success, responseCode, response) =>
                {
#if AR_COMPANION_DATA_LOG
                Debug.Log(success ? "Got cloud resource list" : "Failed to get cloud resource list");
#endif

                    // Only overwrite on failure if response is 404--this means the resource folder is brand new
                    if (responseCode == 404L)
                    {
                        response = null;
                    }
                    else if (!success)
                    {
                        if (s_FallbackCloudResourceList == null)
                            s_FallbackCloudResourceList = GetFallbackCloudResourceList(project, resourceFolder);

                        if (s_FallbackCloudResourceList == null)
                            s_FallbackCloudResourceList = new ResourceList();

                        callback(false, s_FallbackCloudResourceList);
                        return;
                    }

                    ResourceList resourceList;
                    try
                    {
                        resourceList = SceneSerialization.FromJson<ResourceList>(response);
                    }
                    catch
                    {
                        resourceList = new ResourceList();
                    }

                    s_FallbackCloudResourceList = resourceList;
                    WriteFallbackCloudResourceList(project, resourceFolder, s_FallbackCloudResourceList);
                    resourceList.SetCloudResource(true);
                    callback(true, resourceList);
                });
        }

        internal static void GetResourceList(this IUsesCloudStorage storageUser, CompanionProject project,
            string resourceFolder, bool showIssueDialog, Action<bool, ResourceList> callback)
        {
            if (callback == null)
            {
                Debug.LogWarning("Callback is null in GetMergedResourceList");
                return;
            }

            if (!project.linked)
            {
                callback(false, GetLocalResourceList(project, resourceFolder));
                return;
            }

            storageUser.GetCloudResourceList(project, resourceFolder, showIssueDialog, (success, cloudResourceList) =>
            {
                var localResourceList = GetLocalResourceList(project, resourceFolder);
                var merged = ResourceList.MergeResourceLists(localResourceList, cloudResourceList);
                callback(success, merged);
            });
        }

        // TODO: Cache local resource lists in memory
        static ResourceList GetLocalResourceList(CompanionProject project, string resourceFolder)
        {
            return GetResourceListFromLocalStorage(project, resourceFolder, k_LocalResourceListFilename);
        }

        static ResourceList GetFallbackCloudResourceList(CompanionProject project, string resourceFolder)
        {
            var resourceList = GetResourceListFromLocalStorage(project, resourceFolder, k_FallbackCloudResourceListFilename);
            resourceList?.SetCloudResource(true);
            return resourceList;
        }

        static ResourceList GetResourceListFromLocalStorage(CompanionProject project, string resourceFolder, string filename)
        {
            try
            {
                var projectFolder = project.GetLocalPath();
                var folder = Path.Combine(projectFolder, resourceFolder);
                var path = Path.Combine(folder, filename);

#if AR_COMPANION_DATA_LOG
                Debug.Log("Read resource list from path " + path);
#endif

                if (File.Exists(path))
                {
                    var jsonText = File.ReadAllText(path);
                    if (!string.IsNullOrEmpty(jsonText))
                        return SceneSerialization.FromJson<ResourceList>(jsonText);
                }
            }
            catch (Exception e)
            {
                CompanionIssueUtils.HandleIssue(CoreIssueCodes.CompanionFileRead, e);
            }

            return null;
        }

        internal static RequestHandle WriteCloudResourceList(this IUsesCloudStorage storageUser, CompanionProject project,
            string resourceFolder, ResourceList resourceList, Action<bool> callback = null)
        {
            if (resourceList.Merged)
            {
                Debug.LogError("Tried to write merged resource list to cloud storage--this should never happen");
                return default;
            }

            var value = SceneSerialization.ToJson(resourceList);
            var key = GetResourceListKey(project.index, resourceFolder);
            return storageUser.CloudSaveAsync(key, value, true, (success, responseCode, _) =>
            {
#if AR_COMPANION_DATA_LOG
                Debug.Log(success ? $"Resource list saved: {key}" : $"Failed to save resource list: {key}");
#endif

                if (success)
                    WriteFallbackCloudResourceList(project, resourceFolder, resourceList);

                callback?.Invoke(success);

                if (!success && storageUser.HasValidIdentity())
                    CompanionIssueUtils.HandleIssue(CoreIssueCodes.CompanionUploadFailed);
            });
        }

        static void WriteLocalResourceList(CompanionProject project, string resourceFolder, ResourceList resourceList)
        {
            WriteResourceListToLocalStorage(project, resourceFolder, resourceList, k_LocalResourceListFilename);
        }

        static void WriteFallbackCloudResourceList(CompanionProject project, string resourceFolder, ResourceList resourceList)
        {
            WriteResourceListToLocalStorage(project, resourceFolder, resourceList, k_FallbackCloudResourceListFilename);
        }

        static void WriteResourceListToLocalStorage(CompanionProject project, string resourceFolder, ResourceList resourceList, string filename)
        {
            if (resourceList.Merged)
            {
                Debug.LogError("Tried to write merged resource list to local storage--this should never happen");
                return;
            }

            // Don't write local resource list in edit mode to make it easier to test sync features
            if (!Application.isPlaying)
                return;

            var projectFolder = project.GetLocalPath();
            var folder = Path.Combine(projectFolder, resourceFolder);
            var path = Path.Combine(folder, filename);

#if AR_COMPANION_DATA_LOG
                Debug.Log("Write resource list to path " + path);
#endif

            try
            {
                if (File.Exists(path))
                    File.Delete(path);

                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                var jsonText = SceneSerialization.ToJson(resourceList);
                File.WriteAllText(path, jsonText);
            }
            catch (Exception e)
            {
                Debug.Log($"Error writing file at {path}");
                CompanionIssueUtils.HandleIssue(CoreIssueCodes.CompanionFileWrite, e);
            }
        }

        static void ModifyResourceList(this IUsesCloudStorage storageUser, CompanionProject project,
            string resourceFolder, bool showIssueDialog, Action<bool, ResourceList> callback, Stack<RequestHandle> requests,
            Action<ResourceList, bool> modifyAction) // modifyAction bool argument is true if we are modifying the cloud list
        {
            if (modifyAction == null)
            {
                Debug.LogWarning("Callback is null in GetCloudResourceList");
                return;
            }

            // Get the latest list to make sure we don't lose scenes in case of stale data
            var localResourceList = GetLocalResourceList(project, resourceFolder);
            if (localResourceList != null)
            {
                modifyAction(localResourceList, false);
                WriteLocalResourceList(project, resourceFolder, localResourceList);
            }

            if (!project.linked)
            {
                ResourceListChanged?.Invoke(true, localResourceList);
                callback.Invoke(true, localResourceList);
                return;
            }

            requests.Push(storageUser.GetCloudResourceList(project, resourceFolder, showIssueDialog, (success, resourceList) =>
            {
                if (success)
                {
                    modifyAction(resourceList, true);
                    requests.Push(storageUser.WriteCloudResourceList(project, resourceFolder, resourceList, writeListSuccess =>
                    {
#if UNITY_EDITOR
                        if (writeListSuccess)
                            CloudResourceListChanged?.Invoke(resourceList);
#endif

                        resourceList = ResourceList.MergeResourceLists(localResourceList, resourceList);
                        ResourceListChanged?.Invoke(true, resourceList);
                        callback?.Invoke(writeListSuccess, resourceList);
                    }));
                }
                else
                {
                    modifyAction(resourceList, false);
                    resourceList = ResourceList.MergeResourceLists(localResourceList, resourceList);
                    ResourceListChanged?.Invoke(false, resourceList);
                    callback?.Invoke(false, resourceList);
                }
            }));
        }

        internal static void UploadResource(this IUsesCloudStorage storageUser, CompanionProject project,
            string resourceFolder, CompanionResource resource, Stack<RequestHandle> requests,
            Action<bool, ResourceList> callback = null)
        {
            ResourceSyncEvent.SendEvent(SyncAction.Upload, resource.key, resource.type, resource.fileSize);

            void OnSave(bool success)
            {
                storageUser.ModifyResourceList(project, resourceFolder, true, callback, requests,
                    (resourceList, _) =>
                    {
                        resourceList.AddOrOverwriteExistingResource(resource);
                    });
            }

            if (resource.isCloudResource)
            {
                Debug.LogError("Trying to upload a resource which is already a cloud resource--this should never happen");
                return;
            }

            switch (resource.type)
            {
                case ResourceType.Scene:
                    requests.Push(storageUser.UploadScene(project, resource, OnSave));
                    break;
                case ResourceType.Environment:
                    requests.Push(storageUser.UploadEnvironment(project, resource, OnSave));
                    break;
                case ResourceType.Recording:
                    storageUser.UploadDataRecording(project, resource, requests, OnSave);
                    break;
                case ResourceType.Marker:
                    storageUser.UploadMarker(project, resource, requests, OnSave);
                    break;
                case ResourceType.Prefab:
                    requests.Push(storageUser.UploadPrefab(project, resource, OnSave));
                    break;
                case ResourceType.CapturedObject:
                    storageUser.UploadCapturedObject(project, resource, requests, OnSave);
                    break;
                default:
                    UnknownIssueHandler(CoreIssueCodes.CompanionUnknownResourceUpload, "upload", resource.type);
                    break;
            }
        }

        internal static void DownloadResource(this IUsesCloudStorage storageUser, CompanionProject project,
            string resourceFolder, CompanionResource resource, Stack<RequestHandle> requests,
            Action<bool, ResourceList> callback = null)
        {
            ResourceSyncEvent.SendEvent(SyncAction.Download, resource.key, resource.type, resource.fileSize);

            void OnSave(bool success)
            {
                if (!success)
                {
                    callback?.Invoke(false, null);
                    CompanionIssueUtils.HandleIssue(CoreIssueCodes.CompanionDownloadFailed);
                    return;
                }

                if (!resource.isCloudResource)
                {
                    resource = resource.cloudVersion;
                    if (resource == null)
                    {
                        Debug.LogError("Trying to download a resource but no cloud resource exists");
                        return;
                    }
                }

                storageUser.ModifyResourceList(project, resourceFolder, true, callback, requests,
                    (resourceList, _) => { resourceList.AddOrOverwriteExistingResource(resource); });
            }

            switch (resource.type)
            {
                case ResourceType.Scene:
                    requests.Push(storageUser.DownloadScene(project, resource, OnSave));
                    break;
                case ResourceType.Environment:
                case ResourceType.Recording:
                case ResourceType.Marker:
                    // Do nothing--these resource types cannot be viewed on device
                    break;
                case ResourceType.Prefab:
                    requests.Push(storageUser.DownloadPrefab(project, resource, OnSave));
                    break;
                default:
                    UnknownIssueHandler(CoreIssueCodes.CompanionUnknownResourceDownload, "download", resource.type);
                    break;
            }
        }

        internal static void DeleteResource(this IUsesCloudStorage storageUser, CompanionProject project,
            string resourceFolder, CompanionResource resource, Stack<RequestHandle> requests,
            Action<bool, ResourceList> callback = null)
        {
            ResourceSyncEvent.SendEvent(SyncAction.Remove, resource.key, resource.type, resource.fileSize);

            var cloudOnly = resource.isCloudResource && resource.cloudVersion == null;
            if (!cloudOnly)
                DeleteLocalResource(project, resource, resourceFolder);

            storageUser.ModifyResourceList(project, resourceFolder, true, callback, requests, (resourceList, success) =>
            {
                // Only attempt to delete cloud resource if we have successfully modified cloud resource list
                if (success)
                    storageUser.DeleteCloudResource(resource, resourceList, requests);

                // Remove resource if local-only or if cloud resource could not be removed
                if (success || resource.cloudVersion == null && !resource.isCloudResource)
                    resourceList.RemoveResource(resource.index);
            });
        }

        internal static void DeleteResources(this IUsesCloudStorage storageUser, CompanionProject project,
            string resourceFolder, List<CompanionResource> resources, Stack<RequestHandle> requests,
            Action<bool, ResourceList> callback = null)
        {
            var shouldUpdateCloud = false;
            foreach (var resource in resources)
            {
                shouldUpdateCloud |= resource.isCloudResource;
                ResourceSyncEvent.SendEvent(SyncAction.Remove, resource.key, resource.type, resource.fileSize);

                DeleteLocalResource(project, resource, resourceFolder);
            }

            storageUser.ModifyResourceList(project, resourceFolder, shouldUpdateCloud, callback, requests, (resourceList, success) =>
            {
                // Only attempt to delete cloud resources if we have successfully modified cloud resource list
                if (success)
                {
                    foreach (var resource in resources)
                    {
                        storageUser.DeleteCloudResource(resource, resourceList, requests);
                    }
                }

                resourceList.RemoveResources(resources);
            });
        }

        static void DeleteLocalResource(CompanionProject project, CompanionResource resource, string resourceFolder)
        {
            var resourceKey = resource.key;
            var thumbnailKey = GetThumbnailKey(resourceKey);
            var folder = GetLocalResourceFolderPath(project, resourceFolder, ThumbnailResourceSubFolder);
            var path = Path.Combine(folder, string.Format(k_ImageFileFormat, thumbnailKey));
            if (File.Exists(path))
                File.Delete(path);

            switch (resource.type)
            {
                case ResourceType.Scene:
                    CompanionSceneUtils.DeleteLocalScene(project, resourceKey);
                    break;
                case ResourceType.Environment:
                    CompanionEnvironmentUtils.DeleteLocalEnvironment(project, resourceKey);
                    break;
                case ResourceType.Recording:
                    CompanionDataRecordingUtils.DeleteLocalDataRecording(project, resourceKey);
                    break;
                case ResourceType.Marker:
                    CompanionMarkerUtils.DeleteLocalMarker(project, resourceKey);
                    break;
                case ResourceType.Prefab:
                    CompanionAssetUtils.DeleteLocalPrefab(project, resourceKey);
                    break;
                case ResourceType.CapturedObject:
                    CompanionObjectCaptureUtils.DeleteLocalCapturedObject(project, resourceKey);
                    break;
                default:
                    UnknownIssueHandler(CoreIssueCodes.CompanionUnknownResourceDelete, "delete", resource.type);
                    break;
            }
        }

        static void DeleteCloudResource(this IUsesCloudStorage storageUser, CompanionResource resource,
            ResourceList resourceList, Stack<RequestHandle> requests)
        {
            var key = resource.key;
            requests.Push(storageUser.CloudSaveAsync(key, string.Empty, true));
            SplitResourceKey(key, out _, out var folder, out var guid);

            // Delete thumbnail
            var thumbnailKey = GetThumbnailKey(key);
            requests.Push(storageUser.CloudSaveAsync(thumbnailKey, string.Empty, false));

            // Some resource types have extra resources
            switch (resource.type)
            {
                case ResourceType.Prefab:
                case ResourceType.Scene:
                    // Asset Bundles
                    resourceList.TryGetBundleGroup(key, out var bundleGroup);
                    bundleGroup?.DeleteBundles(storageUser, requests);
                    break;
                case ResourceType.Recording:
                    // Video
                    var componentKey = CompanionDataRecordingUtils.GetRecordingComponentKey(CompanionDataRecordingUtils.VideoType, folder, guid);
                    requests.Push(storageUser.CloudSaveAsync(componentKey, string.Empty, true));

                    // Camera Path
                    componentKey = CompanionDataRecordingUtils.GetRecordingComponentKey(CompanionDataRecordingUtils.CameraPathType, folder, guid);
                    requests.Push(storageUser.CloudSaveAsync(componentKey, string.Empty, true));

                    // Planes
                    componentKey = CompanionDataRecordingUtils.GetRecordingComponentKey(CompanionDataRecordingUtils.PlaneDataType, folder, guid);
                    requests.Push(storageUser.CloudSaveAsync(componentKey, string.Empty, true));

                    // Point Cloud
                    componentKey = CompanionDataRecordingUtils.GetRecordingComponentKey(CompanionDataRecordingUtils.PointCloudType, folder, guid);
                    requests.Push(storageUser.CloudSaveAsync(componentKey, string.Empty, true));
                    break;
                case ResourceType.Marker:
                    // Image
                    var imageKey = CompanionMarkerUtils.GetMarkerImageKey(folder, guid);
                    requests.Push(storageUser.CloudSaveAsync(imageKey, string.Empty, true));
                    break;
            }
        }

        public static string GetLocalResourceFolderPath(CompanionProject project, string resourceFolder, string type)
        {
            var projectFolder = project.GetLocalPath();
            resourceFolder = GetResourceFolder(resourceFolder);
            return Path.Combine(projectFolder, resourceFolder, type);
        }

        /// <summary>
        /// Get the resource key for the thumbnail image of a companion resource.
        /// For resources without thumbnails, this key will not exist.
        /// </summary>
        static string GetThumbnailKey(string resourceKey) => $"{resourceKey}_{k_ThumbnailKeySuffix}";

        public static void SaveThumbnailImage(this IUsesCloudStorage storageUser, CompanionProject project,
            string resourceFolder, string resourceKey, byte[] thumbnail, bool attemptCloudWrite,
            Stack<RequestHandle> requests,
            Action<bool, string> callback = null)
        {
            var key = GetThumbnailKey(resourceKey);
            WriteLocalImage(project, resourceFolder, key, thumbnail, (imageWriteSuccess, _) =>
            {
                if (!project.linked)
                {
                    callback?.Invoke(true, key);
                    return;
                }

                if (attemptCloudWrite)
                {
                    requests.Push(storageUser.CloudSaveAsync(key, thumbnail, false,
                        (success, responseCode, response) =>
                        {
                            callback?.Invoke(success, key);
                        }));
                }
                else
                {
                    callback?.Invoke(false, key);
                }
            });
        }

        static void WriteLocalImage(CompanionProject project, string resourceFolder, string resourceKey, byte[] png, Action<bool, string> callback = null)
        {
            var folder = GetLocalResourceFolderPath(project, resourceFolder, ThumbnailResourceSubFolder);
            var path = Path.Combine(folder, string.Format(k_ImageFileFormat, resourceKey));

#if AR_COMPANION_DATA_LOG
            Debug.Log($"Write PNG to path {path}");
#endif
            CoroutineUtils.StartCoroutine(CompanionFileUtils.WriteFileAsync(path, png, callback));
        }

        public static void GetThumbnailImage(this IUsesCloudStorage storageUser, CompanionProject project,
            string resourceKey, long timestamp, Stack<RequestHandle> requests, Action<bool, Texture2D> callback = null)
        {
            SplitResourceKey(resourceKey, out var group, out var resourceFolder, out var guid);
            if (string.IsNullOrEmpty(group) || string.IsNullOrEmpty(guid))
                return;

            var thumbnailKey = GetThumbnailKey(resourceKey);

            // TODO: validate names for path errors
            // Write to local storage in case cloud isn't reachable
            var folder = GetLocalResourceFolderPath(project, resourceFolder, ThumbnailResourceSubFolder);
            var path = Path.Combine(folder, string.Format(k_ImageFileFormat, thumbnailKey));
            if (!project.linked)
            {
                requests.Push(storageUser.GetLocalThumbnail(path, callback));
                return;
            }

            requests.Push(storageUser.CloudLoadTextureAsync(thumbnailKey, false,
                (success, responseCode, texture, response) =>
                {
                    if (success)
                    {
                        callback?.Invoke(true, texture);

                        // Write to local storage in case cloud isn't reachable
                        if (File.Exists(path))
                            File.Delete(path);

                        if (!Directory.Exists(folder))
                            Directory.CreateDirectory(folder);

                        CoroutineUtils.StartCoroutine(CompanionFileUtils.WriteFileAsync(path, response, (fileWriteSuccess, _) =>
                        {
                            if (fileWriteSuccess && timestamp > 0)
                            {
                                try
                                {
                                    File.SetLastWriteTimeUtc(path, new DateTime(timestamp));
                                }
                                catch (Exception e)
                                {
                                    Debug.LogError("Error writing thumbnail timestamp");
                                    Debug.LogException(e);
                                }
                            }
                        }));
                    }
                    else
                    {
                        // Fall back to local cache if exists
                        requests.Push(storageUser.GetLocalThumbnail(path, callback));
                    }
                }));
        }

        internal static RequestHandle GetLocalThumbnail(this IUsesCloudStorage storageUser, string path, Action<bool, Texture2D> callback)
        {
            if (!File.Exists(path))
            {
                callback(false, null);
                return default;
            }

            return storageUser.LoadLocalTextureAsync(path, (success, responseCode, texture, _) =>
            {
                callback(success, texture);
            });
        }

        public static void SetCurrentResourceFolder(string resourceFolder)
        {
            PlayerPrefs.SetString(ResourceFolderPrefsKey, resourceFolder);
        }

        // Adapted from https://stackoverflow.com/questions/11/calculate-relative-time-in-c-sharp
        internal static string GetRelativeTime(DateTime time)
        {
            return GetRelativeTime(DateTime.UtcNow.Ticks, time.Ticks);
        }

        internal static string GetRelativeTime(long compareTicks, long timeTicks)
        {
            const int second = 1;
            const int minute = 60 * second;
            const int hour = 60 * minute;
            const int day = 24 * hour;
            const int month = 30 * day;

            var timeSpan = new TimeSpan(compareTicks - timeTicks);
            var delta = Math.Abs(timeSpan.TotalSeconds);

            if (delta < 1 * minute)
                return "Just now";

            if (delta < 2 * minute)
                return "A minute ago";

            if (delta < 45 * minute)
                return timeSpan.Minutes + " minutes ago";

            if (delta < 2 * hour)
                return "An hour ago";

            if (delta < 24 * hour)
                return timeSpan.Hours + " hours ago";

            if (delta < 48 * hour)
                return "Yesterday";

            if (delta < 30 * day)
                return timeSpan.Days + " days ago";

            if (delta < 12 * month)
            {
                var months = Convert.ToInt32(Math.Floor((double)timeSpan.Days / 30));
                return months <= 1 ? "One month ago" : months + " months ago";
            }

            var years = Convert.ToInt32(Math.Floor((double)timeSpan.Days / 365));
            return years <= 1 ? "One year ago" : years + " years ago";
        }
    }
}
