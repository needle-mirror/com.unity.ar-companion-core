using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Unity.AR.Companion.CloudStorage;
using Unity.ListViewFramework;
using UnityEngine;

namespace Unity.AR.Companion.Core
{
    [Serializable]
    class CompanionProject : IListViewItemData<string>
    {
        const string k_Template = "ProjectTile";

        [SerializeField]
        string m_ProjectID;

        [SerializeField]
        string m_Name;

        [SerializeField]
        bool m_Linked;

        public string index { get { return m_ProjectID; } }
        public string name { get { return m_Name; } set { m_Name = value; } }
        public string template { get { return k_Template; } }
        public bool linked { get { return m_Linked; } set { m_Linked = value; } }
        public bool selected { get; set; }
        internal string projectId { set => m_ProjectID = value; }
        internal Texture thumbnail => m_Thumbnail;
        internal string thumbnailDate => m_ThumbnailDate;
        internal bool thumbnailFound => m_ThumbnailFound;

        bool m_ThumbnailRequestStarted;
        bool m_ThumbnailTextureRequestStarted;
        bool m_ThumbnailFound;
        string m_ThumbnailPath;
        Texture m_Thumbnail;
        string m_ThumbnailDate;

        public CompanionProject() { }

        public CompanionProject(string projectID, string name, bool linked)
        {
            m_ProjectID = projectID;
            m_Name = name;
            m_Linked = linked;
        }

        public void Link(string id, string newName)
        {
            m_ProjectID = id;
            m_Name = newName;
            m_Linked = true;
        }

        public void Unlink(string id)
        {
            m_ProjectID = id;
            m_Linked = false;
        }

        internal static string GetProjectPath(string projectId)
        {
            var root = Application.persistentDataPath;
            return Path.Combine(root, projectId);
        }

        public string GetLocalPath() { return GetProjectPath(m_ProjectID); }

        public void GetLatestThumbnailIfNeeded(string resourceList)
        {
            if (m_ThumbnailRequestStarted)
                return;

            m_ThumbnailRequestStarted = true;
            var path = CompanionResourceUtils.GetLocalResourceFolderPath(this, resourceList,
                CompanionResourceUtils.ThumbnailResourceSubFolder);

            if (!Directory.Exists(path))
            {
                // Fall back to default resource folder
                path = CompanionResourceUtils.GetLocalResourceFolderPath(this, string.Empty,
                    CompanionResourceUtils.ThumbnailResourceSubFolder);

                if (!Directory.Exists(path))
                    return;
            }

            new Thread(() =>
            {
                try
                {
                    var directory = new DirectoryInfo(path);
                    var files = new List<FileInfo>(directory.GetFiles());
                    if (files.Count == 0)
                        return;

                    files.Sort((a, b) => b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc));
                    var selectedThumbnail = files[0];
                    m_ThumbnailPath = selectedThumbnail.ToString();
                    m_ThumbnailDate = CompanionResourceUtils.GetRelativeTime(selectedThumbnail.LastWriteTimeUtc);
                }
                catch (Exception e)
                {
                    Debug.LogError("Error getting project thumbnails");
                    Debug.LogException(e);
                }
            }).Start();
        }

        public void UpdateThumbnail(IUsesCloudStorage storageUser)
        {
            if (m_Thumbnail || string.IsNullOrEmpty(m_ThumbnailPath))
                return;

            if (m_ThumbnailTextureRequestStarted)
                return;

            m_ThumbnailTextureRequestStarted = true;
            storageUser.GetLocalThumbnail(m_ThumbnailPath, (success, texture) =>
            {
                m_Thumbnail = texture;
                m_ThumbnailFound = true;
            });
        }
    }
}
