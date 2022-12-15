// #define AR_COMPANION_DATA_LOG

using System;
using System.Collections.Generic;
using System.IO;
using Unity.AR.Companion.CloudStorage;
using Unity.RuntimeSceneSerialization;
using UnityEngine;

#if UNITY_SHARPZIPLIB_1_3_OR_NEWER
using Unity.SharpZipLib.Zip;
#else
using ICSharpCode.SharpZipLib.Zip;
#endif

namespace Unity.AR.Companion.Core
{
    /// <summary>
    /// Utility methods for synchronizing object captures using cloud storage
    /// </summary>
    static class CompanionObjectCaptureUtils
    {
        const string k_CapturedObjectGroupName = "CapturedObjects";
        const string k_ImagePayloadType = "CapturedObjectImages";
        const string k_TmpImageFolderFormat = "Images-{0}";
        const string k_TmpZipFileFormat = "Images{0}-{1}.zip";
        const string k_JsonFileFormat = "{0}.json";

        static string GetCapturedObjectKey(string resourceFolder, string guid) { return $"{k_CapturedObjectGroupName}_{resourceFolder}_{guid}"; }

        static string GetImagePayloadKey(string resourceFolder, string guid) { return $"{k_ImagePayloadType}_{resourceFolder}_{guid}"; }

        internal static string GetTempImageFolderPath() { return Path.Combine(Application.persistentDataPath, "Temp", string.Format(k_TmpImageFolderFormat, DateTime.Now.Ticks)); }

        static string GetTempZipFilePath(int imgCount = -1)
        {
            return Path.Combine(Application.persistentDataPath,
                "Temp",
                string.Format(k_TmpZipFileFormat, imgCount == -1 ? string.Empty : imgCount.ToString(), DateTime.Now.Ticks));
        }

        internal static void SplitCapturedObjectKey(string key, out string resourceFolder, out string guid)
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

        internal static string CompressImageFolder(string folderPath, out long fileSize)
        {
            if (!Directory.Exists(folderPath))
            {
                Debug.LogError("Image directory not found");
                fileSize = 0;
                return null;
            }

            var buffer = new byte[4096];
            var directory = new DirectoryInfo(folderPath);
            var zipFilePath = GetTempZipFilePath();
            var outputFile = File.Create(zipFilePath);

            var zipStream = new ZipOutputStream(outputFile);
            zipStream.SetLevel(9);

            var dateTime = DateTime.Now;
            foreach (var file in directory.EnumerateFiles())
            {
                var zipEntry = new ZipEntry(file.Name);
                zipEntry.DateTime = dateTime;
                zipStream.PutNextEntry(zipEntry);

                using (var fileStream = File.OpenRead(file.FullName))
                {
                    int sourceBytes;
                    do
                    {
                        sourceBytes = fileStream.Read(buffer, 0, buffer.Length);
                        zipStream.Write(buffer, 0, sourceBytes);
                    } while (sourceBytes > 0);
                }
            }

            fileSize = outputFile.Length;
            zipStream.Finish();
            zipStream.Close();

            return zipFilePath;
        }

        static RequestHandle SaveCapturedObject(this IUsesCloudStorage storageUser, CompanionProject project, string resourceFolder,
            string guid, CapturedObject recording, Action<bool, string, long> callback, ProgressCallback progress)
        {
            var jsonText = SceneSerialization.ToJson(recording);
            WriteLocalCapturedObject(project, resourceFolder, guid, jsonText);

            var key = GetCapturedObjectKey(resourceFolder, guid);

            if (!project.linked)
            {
                callback?.Invoke(true, key, jsonText.Length);
                return default;
            }

            return storageUser.CloudSaveAsync(key, jsonText, true, (success, responseCode, _) =>
            {
#if AR_COMPANION_DATA_LOG
                Debug.LogFormat(success ? "Data recording {0} saved" : "Failed to save data recording {0}", guid);
#endif
                callback?.Invoke(success, key, jsonText.Length);
            }, progress);
        }

        static void WriteLocalCapturedObject(CompanionProject project, string resourceFolder, string guid, string jsonText)
        {
            // Write to local storage in case cloud isn't reachable
            var folder = CompanionResourceUtils.GetLocalResourceFolderPath(project, resourceFolder, k_CapturedObjectGroupName);
            var filename = string.Format(k_JsonFileFormat, guid);
            var path = Path.Combine(folder, filename);

#if AR_COMPANION_DATA_LOG
            Debug.Log("Write prefab to path " + path);
#endif

            CoroutineUtils.StartCoroutine(CompanionFileUtils.WriteFileAsync(path, jsonText));
        }

        internal static void SaveCapturedObjectAndUpdateResourceList(this IUsesCloudStorage storageUser,
            CompanionProject project, string resourceFolder, string name, string guid,
            CapturedObject capture, long zipFileSize, Stack<RequestHandle> requests, byte[] thumbnail = null,
            Action<bool, bool, string, ResourceList> callback = null, ProgressCallback progress = null)
        {
            requests.Push(SaveCapturedObject(storageUser, project, resourceFolder, guid, capture,
                (recordingSuccess, key, recordingFileSize) =>
                {
                    var totalFileSize = recordingFileSize + zipFileSize;
                    if (thumbnail != null)
                    {
                        CompanionResourceUtils.SaveThumbnailImage(storageUser, project, resourceFolder, key, thumbnail, recordingSuccess,
                            requests, (thumbnailSuccess, _) =>
                            {
                                storageUser.AddOrUpdateResource(project, resourceFolder, key, name,
                                    ResourceType.CapturedObject, totalFileSize, false, thumbnailSuccess, requests,
                                    (writeListSuccess, resourceList) =>
                                    {
                                        callback?.Invoke(true, writeListSuccess, key, resourceList);
                                    });
                            });
                    }
                    else
                    {
                        storageUser.AddOrUpdateResource(project, resourceFolder, key, name,
                            ResourceType.CapturedObject, totalFileSize, false, recordingSuccess, requests,
                            (writeListSuccess, resourceList) =>
                            {
                                callback?.Invoke(true, writeListSuccess, key, resourceList);
                            });
                    }
                }, progress));
        }

        internal static RequestHandle UploadCapturedObjectArchive(this IUsesCloudStorage storageUser, string guid, string projectFolder,
            string resourceFolder, Action<bool> callback = null, ProgressCallback progress = null)
        {
            const int timeout = 0; // Recording components may be quite large and take a long time to download
            var path = GetImageArchivePath(GetCapturedObjectFolder(projectFolder, resourceFolder), guid);
            var key = GetImagePayloadKey(resourceFolder, guid);
            if (!File.Exists(path))
            {
                Debug.LogError("Component file not found at path " + path);
                return default;
            }

            return storageUser.CloudSaveFileAsync(key, path, true, (success, responseCode, _) =>
            {
#if AR_COMPANION_DATA_LOG
                Debug.LogFormat(success ? "Component {0} saved" : "Failed to save captured object {0}", key);
#endif
                callback?.Invoke(success);
            }, progress, timeout);
        }

        internal static RequestHandle DownloadCapturedObjectArchive(this IUsesCloudStorage storageUser, string resourceKey, string projectFolder,
            Action<bool, string> callback = null, ProgressCallback progress = null)
        {
            const int timeout = 0; // Archives may be quite large and take a long time to download
            SplitCapturedObjectKey(resourceKey, out var resourceFolder, out var guid);
            var path = GetImageArchivePath(GetCapturedObjectFolder(projectFolder, resourceFolder), guid);
            var key = GetImagePayloadKey(resourceFolder, guid);

            return storageUser.CloudLoadAsync(key, true, (success, _, response) =>
            {
#if AR_COMPANION_DATA_LOG
                Debug.LogFormat(success ? "Component {0} saved" : "Failed to download captured object {0}", key);
#endif

                if (success)
                {
                    var directoryName = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(directoryName) && !Directory.Exists(directoryName))
                        Directory.CreateDirectory(directoryName);

                    File.WriteAllBytes(path, response);
                }

                callback?.Invoke(success, path);
            }, progress, timeout);
        }

        internal static void UploadCapturedObject(this IUsesCloudStorage storageUser, CompanionProject project,
            CompanionResource resource, Stack<RequestHandle> requests, Action<bool> callback = null)
        {
            try
            {
                if (!project.linked)
                {
                    Debug.LogError("Cannot upload data recording in unlinked project");
                    callback?.Invoke(false);
                    return;
                }

                var key = resource.key;
                SplitCapturedObjectKey(key, out var resourceFolder, out var guid);
                var projectFolder = project.GetLocalPath();
                var capturedObjectName = resource.name;
                requests.Push(storageUser.UploadCapturedObjectArchive(guid, projectFolder, resourceFolder, _ =>
                {
                    var capturedObject = new CapturedObject(capturedObjectName);
                    storageUser.SaveCapturedObjectAndUpdateResourceList(project, resourceFolder, capturedObjectName, guid,
                        capturedObject, resource.fileSize, requests, null,
                        (success, listSuccess, response, list) =>
                        {
                            callback?.Invoke(success);
                            if (!success)
                                CompanionIssueUtils.HandleIssue(CoreIssueCodes.CompanionUploadFailed);
                        });
                }));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                CompanionIssueUtils.HandleIssue(CoreIssueCodes.CompanionUploadFailed);
            }
        }

        static string GetCapturedObjectFolder(string projectFolder, string resourceFolder) { return Path.Combine(projectFolder, resourceFolder, "CapturedObjects"); }

        static string GetImageArchivePath(string folder, string guid) { return Path.Combine(folder, $"{guid}.zip"); }

        internal static void MoveTempImageArchive(string tempFilePath, string projectFolder, string resourceFolder, string guid)
        {
            if (File.Exists(tempFilePath))
            {
                var folder = GetCapturedObjectFolder(projectFolder, resourceFolder);
                var path = GetImageArchivePath(folder, guid);

                if (File.Exists(path))
                    File.Delete(path);

                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                File.Move(tempFilePath, path);
                return;
            }

            Debug.LogErrorFormat("Error: Could not find image archive file at path {0}", tempFilePath);
        }

        public static void DeleteTempImages(string path)
        {
            if (!Directory.Exists(path))
                return;

            Directory.Delete(path, true);
        }

        public static void DeleteLocalCapturedObject(CompanionProject project, string key)
        {
            SplitCapturedObjectKey(key, out var resourceFolder, out var guid);
            var folder = CompanionResourceUtils.GetLocalResourceFolderPath(project, resourceFolder, k_CapturedObjectGroupName);
            var filename = string.Format(k_JsonFileFormat, guid);
            var path = Path.Combine(folder, filename);

#if AR_COMPANION_DATA_LOG
            Debug.Log($"Delete captured object at path {path}");
#endif

            if (File.Exists(path))
                File.Delete(path);

            var archivePath = GetImageArchivePath(folder, guid);

#if AR_COMPANION_DATA_LOG
            Debug.Log($"Delete captured object at path {archivePath}");
#endif

            if (File.Exists(archivePath))
                File.Delete(archivePath);
        }
    }
}
