using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AR.Companion.CloudStorage;
using Unity.RuntimeSceneSerialization;
using Unity.XRTools.ModuleLoader;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Timeline;
using UnityEngine.UIElements;
using UnityObject = UnityEngine.Object;

#if INCLUDE_MARS
using Unity.MARS.Data;
#endif

namespace Unity.AR.Companion.Core
{
    class CompanionResourceUI : EditorWindow, IUsesCloudStorage
    {
        struct ResourceListUpdate
        {
            public string ResourceKey;
            public string BundleKey;
            public long FileSize;
            public string Platform;
        }

        internal const string ColumnBundleClassName = "column_bundle";

        const string k_AssetBundleSyncTitle = "Asset Bundle Sync";
        const string k_AssetBundleSyncMessage =
            "You are about to export AssetBundles for the selected assets on the selected platforms.\n" +
            "This may require switching platforms and could take a long time depending on your project size.\n\n" +
            "Do you wish to continue?";
        const string k_OkButtonTextAssetBundleSync = "Sync Bundles";
        const string k_CancelButtonTextAssetBundleSync = "Cancel";

        const string k_InternalIosTextureIconStr = "BuildSettings.iPhone";
        const string k_InternalAndroidTextureIconStr = "BuildSettings.Android";

        const string k_CompanionResourceUIXmlPath = "Packages/com.unity.ar-companion-core/Editor/CompanionResourceWindow/CompanionResourceUI.uxml";
        const string k_CompanionResourceItemXmlPath = "Packages/com.unity.ar-companion-core/Editor/CompanionResourceWindow/CompanionResourceItem.uxml";

        const string k_UnlinkedProjectMessageMessageXmlName = "unlinkedProjectMessage";
        const string k_NotSignedInMessageXmlName = "notSignedInMessage";
        const string k_QrCodeButtonXmlName = "QRCodeButton";
        const string k_ReloadProjectButtonXmlName = "reloadProjectButton";
        const string k_ResourceFolderTextFieldXmlName = "resourceFolderTextField";
        const string k_LagCompensationFieldXmlName = "lagCompensationField";
        const string k_ProjectNameLabelXmlName = "projectNameLabel";
        const string k_ProjectIDTextFieldXmlName = "projectIDTextField";
        const string k_LastSyncedLabelXmlName = "lastSyncedLabel";
        const string k_InfoLabelXmlName = "infoLabel";

        const string k_IosTitleToggleButtonXmlName = "iOSTitleToggleButton";
        const string k_AndroidTitleToggleButtonXmlName = "androidTitleToggleButton";

        const string k_ScenesFoldoutXmlName = "scenesFoldout";
        const string k_ScenesResourceItemsAreaXmlName = "scenesResourceItemsArea";

        const string k_EnvironmentsFoldoutXmlName = "environmentsFoldout";
        const string k_EnvironmentsResourceItemsAreaXmlName = "environmentsResourceItemsArea";

        const string k_RecordingsFoldoutXmlName = "recordingsFoldout";
        const string k_RecordingsResourceItemsAreaXmlName = "recordingsResourceItemsArea";

        const string k_MarkersFoldoutXmlName = "markersFoldout";
        const string k_MarkersResourceItemsAreaXmlName = "markersResourceItemsArea";

        const string k_PrefabsFoldoutXmlName = "prefabsFoldout";
        const string k_PrefabsResourceItemsAreaXmlName = "prefabsResourceItemsArea";

        const string k_CapturedObjectsFoldoutXmlName = "capturedObjectsFoldout";
        const string k_CapturedObjectsResourceItemsAreaXmlName = "capturedObjectsResourceItemsArea";

        const string k_SyncAssetBundlesButtonXmlName = "syncBundlesButton";
        const string k_SyncAllButtonXmlName = "syncAllButton";

        const string k_ImageMarkerLibraryImGUIContainerXmlName = "imageMarkerGUIContainer";

        // Companion Resource Item UI
        const string k_MainScrollViewXMLName = "mainScrollView";
        const string k_ScrollbarSpacerXMLName = "scrollbarSpacer";
        const string k_NotSupportedBuildTargetMessageXmlName = "notSupportedBuildTargetMessage";
        const string k_AssetNameLabelXmlName = "assetNameLabel";
        const string k_SizeLabelLabelXmlName = "sizeLabel";
        const string k_DateLabelXmlName = "dateLabel";
        const string k_AssetReferenceObjectFieldXmlName = "assetReferenceObjectField";
        const string k_SyncButtonXmlName = "syncButton";
        const string k_ProgressBarXmlName = "downloadProgressContainer";
        const string k_DeleteCompanionResourceButtonXmlName = "deleteCompanionResourceItemButton";
        const string k_ProgressLabelFormat = "Uploading... {0:F2}%";
        const string k_LastUpdatedFormat = "Last updated {0}";
        const string k_DarkClassName = "dark";
        const string k_LightClassName = "light";
        const string k_RefreshingListText = "Refreshing List...";
        const string k_PrefabSyncAction = "Preview";
        const string k_SceneSyncAction = "Open";
        const string k_PrefabTestDisableReason = "This prefab cannot be previewed because there is no asset bundle for the current platform";
        const string k_CompanionResourceItemXmlName = "companionResourceItem";

#if OBJECT_CAPTURE_AVAILABLE && UNITY_EDITOR_OSX
        const string k_CapturedObjectSyncAction = "Create";
#else
        const string k_CapturedObjectSyncAction = "Download";
#endif

        const string k_CancelButtonText = "Cancel";

        const int k_ProgressBarWidth = 40;

        static readonly GUILayoutOption k_MarkerLibraryLabelWidth = GUILayout.Width(140);
        static readonly GUIContent k_SelectMarkerLibraryContent = new GUIContent("Image marker library",
            "The default image marker library to add newly imported markers to");

        static readonly Color k_LightColor = new Color(0.7f, 0.7f, 0.7f);
        static readonly Color k_DarkColor = new Color(0.25f, 0.25f, 0.25f);

        static readonly Vector2 k_MinWindowSize = new Vector2(600, 50);

        static readonly RuntimePlatform[] k_SupportedPlatforms =
        {
            RuntimePlatform.IPhonePlayer,
            RuntimePlatform.Android
        };

        static bool s_IsIosSupported;
        static bool s_IsAndroidSupported;

#if INCLUDE_MARS
        string[] m_MarkerLibraryNames = { };
        MarsMarkerLibrary[] m_MarkerLibraries;
#endif

        VisualElement m_RootVisualElement;
        VisualTreeAsset m_ResourceItemVisualTreeAsset;

        Label m_UnlinkedProjectMessageLabel;
        Label m_NotSignedInMessageLabel;
        VisualElement m_NotSupportedBuildTargetVisualElement;

        Foldout m_ScenesFoldout;
        VisualElement m_ScenesResourceItemsAreaVisualElement;
        Foldout m_EnvironmentsFoldout;
        VisualElement m_EnvironmentsResourceItemsAreaVisualElement;
        Foldout m_RecordingsFoldout;
        VisualElement m_RecordingsResourceItemsAreaVisualElement;
        Foldout m_MarkersFoldout;
        VisualElement m_MarkersResourceItemsAreaVisualElement;
        Foldout m_PrefabsFoldout;
        VisualElement m_PrefabsResourceItemsAreaVisualElement;
        Foldout m_CapturedObjectsFoldout;
        VisualElement m_CapturedObjectsResourceItemsAreaVisualElement;

        bool m_ToggleStateIosPlatform;
        bool m_ToggleStateAndroidPlatform;

        Label m_LastSyncedLabel;

        Button m_SyncAssetBundlesButton;
        Label m_InfoLabel;

        CompanionProject m_CompanionProject;

#pragma warning disable 649
        [SerializeField]
        GameObject m_SimulatedPlanePrefab;

        [SerializeField]
        Material m_SimulatedPlaneMaterial;

        [SerializeField]
        Post m_PostPrefab;

        [SerializeField]
        MeshFilter m_MeshPrefab;

        [SerializeField]
        GameObject m_SimulatedLightingPrefab;

        [SerializeField]
        float m_LagCompensation = 0.33f;
#pragma warning restore 649

        [NonSerialized]
        bool m_Setup;

        [NonSerialized]
        List<CompanionResource> m_SortedResourceList;

        ResourceList m_ResourceList;

        DateTime m_LastSync;
        int m_UploadCount;
        bool m_UpdatingResourceList;

        readonly Dictionary<RuntimePlatform, List<AssetBundleButton>> m_AssetBundleButtons = new Dictionary<RuntimePlatform, List<AssetBundleButton>>();
        readonly Dictionary<string, AssetImporter> m_Importers = new Dictionary<string, AssetImporter>();
        readonly Dictionary<RuntimePlatform, List<AssetBundleBuild>> m_BuildDictionary = new Dictionary<RuntimePlatform, List<AssetBundleBuild>>();
        readonly Dictionary<string, float> m_UploadProgress = new Dictionary<string, float>();
        readonly List<ResourceListUpdate> m_ResourceListUpdates = new List<ResourceListUpdate>();
        readonly Dictionary<string, ResourceSyncState> m_ResourceSyncStates = new Dictionary<string, ResourceSyncState>();
        readonly Dictionary<string, Button> m_ResourceButtons = new Dictionary<string, Button>();
        ScrollView m_MainScrollView;
        VisualElement m_ScrollbarSpacer;

#if !FI_AUTOFILL
        IProvidesCloudStorage IFunctionalitySubscriber<IProvidesCloudStorage>.provider { get; set; }
#endif

        [MenuItem("Window/AR Companion/Resource Manager")]
         static void OpenCompanionResourceWindow()
         {
             var window = GetWindow<CompanionResourceUI>();
             window.titleContent = new GUIContent("Companion Resources");
             window.minSize = k_MinWindowSize;
         }

        void SetupUI()
        {
            m_CompanionProject = new CompanionProject(Application.cloudProjectId, Application.productName, true);

            s_IsIosSupported = BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.iOS, BuildTarget.iOS);
            s_IsAndroidSupported = BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android);

            m_RootVisualElement = rootVisualElement;
            rootVisualElement.AddToClassList(EditorGUIUtility.isProSkin ? k_DarkClassName : k_LightClassName);
            m_RootVisualElement.Clear();

            m_ResourceItemVisualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_CompanionResourceItemXmlPath);

            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_CompanionResourceUIXmlPath);
            m_RootVisualElement.Add(visualTree.CloneTree());

            m_MainScrollView = m_RootVisualElement.Q<ScrollView>(k_MainScrollViewXMLName);
            m_ScrollbarSpacer = m_RootVisualElement.Q<VisualElement>(k_ScrollbarSpacerXMLName);

            var resourceFolderTextField = m_RootVisualElement.Q<TextField>(k_ResourceFolderTextFieldXmlName);
            resourceFolderTextField.value = PlayerPrefs.GetString(CompanionResourceUtils.ResourceFolderPrefsKey);
            resourceFolderTextField.RegisterValueChangedCallback(value =>
            {
                CompanionResourceUtils.SetCurrentResourceFolder(value.newValue);
                GetResourceList(m_CompanionProject);
            });

            var lagCompensationField = m_RootVisualElement.Q<FloatField>(k_LagCompensationFieldXmlName);
            lagCompensationField.value = m_LagCompensation;
            lagCompensationField.RegisterValueChangedCallback(value =>
            {
                m_LagCompensation = value.newValue;
            });

            m_LastSyncedLabel = m_RootVisualElement.Q<Label>(k_LastSyncedLabelXmlName);
            m_UnlinkedProjectMessageLabel = m_RootVisualElement.Q<Label>(k_UnlinkedProjectMessageMessageXmlName);
            m_NotSignedInMessageLabel = m_RootVisualElement.Q<Label>(k_NotSignedInMessageXmlName);
            m_NotSupportedBuildTargetVisualElement = m_RootVisualElement.Q<VisualElement>(k_NotSupportedBuildTargetMessageXmlName);

#if INCLUDE_MARS
            var imageMarkerImGUIContainer = m_RootVisualElement.Q<IMGUIContainer>(k_ImageMarkerLibraryImGUIContainerXmlName);
            imageMarkerImGUIContainer.onGUIHandler = OnImageMarkerImGUI;
#endif

            m_RootVisualElement.Q<Button>(k_SyncAllButtonXmlName).clicked += OnSyncAllButtonClick;
            m_RootVisualElement.Q<Button>(k_ReloadProjectButtonXmlName).clicked += OnReloadProjectButtonClick;
            m_RootVisualElement.Q<Button>(k_QrCodeButtonXmlName).clicked += () => GetWindow<CompanionLinkWindow>(true, CompanionLinkWindow.Title);

            m_SyncAssetBundlesButton = m_RootVisualElement.Q<Button>(k_SyncAssetBundlesButtonXmlName);
            m_SyncAssetBundlesButton.clicked += OnSyncAssetBundlesButtonClick;

            m_RootVisualElement.Q<Label>(k_ProjectNameLabelXmlName).text = Application.productName;

            var projectIDLabel = m_RootVisualElement.Q<TextField>(k_ProjectIDTextFieldXmlName);
            projectIDLabel.value = Application.cloudProjectId;

            var iOSTitleToggleButton = m_RootVisualElement.Q<Button>(k_IosTitleToggleButtonXmlName);
            iOSTitleToggleButton.style.backgroundImage = new StyleBackground((Texture2D) EditorGUIUtility.IconContent(k_InternalIosTextureIconStr).image);
            iOSTitleToggleButton.clicked += () => OnSelectAllPlatformButtonClick(RuntimePlatform.IPhonePlayer, ref m_ToggleStateIosPlatform);
            iOSTitleToggleButton.SetEnabled(IsPlatformInstalled(RuntimePlatform.IPhonePlayer));

            var androidTitleToggleButton = m_RootVisualElement.Q<Button>(k_AndroidTitleToggleButtonXmlName);
            androidTitleToggleButton.style.backgroundImage = new StyleBackground((Texture2D) EditorGUIUtility.IconContent(k_InternalAndroidTextureIconStr).image);
            androidTitleToggleButton.clicked += () => OnSelectAllPlatformButtonClick(RuntimePlatform.Android, ref m_ToggleStateAndroidPlatform);
            androidTitleToggleButton.SetEnabled(IsPlatformInstalled(RuntimePlatform.Android));

            // Resource Items areas
            m_ScenesResourceItemsAreaVisualElement = m_RootVisualElement.Q<VisualElement>(k_ScenesResourceItemsAreaXmlName);
            m_ScenesFoldout = m_RootVisualElement.Q<Foldout>(k_ScenesFoldoutXmlName);
            m_ScenesFoldout.RegisterValueChangedCallback(val =>
                m_ScenesResourceItemsAreaVisualElement.style.display = val.newValue ? DisplayStyle.Flex : DisplayStyle.None);

            m_MarkersResourceItemsAreaVisualElement = m_RootVisualElement.Q<VisualElement>(k_MarkersResourceItemsAreaXmlName);
            m_MarkersFoldout = m_RootVisualElement.Q<Foldout>(k_MarkersFoldoutXmlName);
            m_MarkersFoldout.RegisterValueChangedCallback(val =>
                m_MarkersResourceItemsAreaVisualElement.style.display = val.newValue ? DisplayStyle.Flex : DisplayStyle.None);

            m_PrefabsResourceItemsAreaVisualElement = m_RootVisualElement.Q<VisualElement>(k_PrefabsResourceItemsAreaXmlName);
            m_PrefabsFoldout = m_RootVisualElement.Q<Foldout>(k_PrefabsFoldoutXmlName);
            m_PrefabsFoldout.RegisterValueChangedCallback(val =>
                m_PrefabsResourceItemsAreaVisualElement.style.display = val.newValue ? DisplayStyle.Flex : DisplayStyle.None);

            m_RecordingsResourceItemsAreaVisualElement = m_RootVisualElement.Q<VisualElement>(k_RecordingsResourceItemsAreaXmlName);
            m_RecordingsFoldout = m_RootVisualElement.Q<Foldout>(k_RecordingsFoldoutXmlName);
            m_RecordingsFoldout.RegisterValueChangedCallback(val =>
                m_RecordingsResourceItemsAreaVisualElement.style.display = val.newValue ? DisplayStyle.Flex : DisplayStyle.None);

            m_EnvironmentsResourceItemsAreaVisualElement = m_RootVisualElement.Q<VisualElement>(k_EnvironmentsResourceItemsAreaXmlName);
            m_EnvironmentsFoldout = m_RootVisualElement.Q<Foldout>(k_EnvironmentsFoldoutXmlName);
            m_EnvironmentsFoldout.RegisterValueChangedCallback(val =>
                m_EnvironmentsResourceItemsAreaVisualElement.style.display = val.newValue ? DisplayStyle.Flex : DisplayStyle.None);

            m_CapturedObjectsResourceItemsAreaVisualElement = m_RootVisualElement.Q<VisualElement>(k_CapturedObjectsResourceItemsAreaXmlName);
            m_CapturedObjectsFoldout = m_RootVisualElement.Q<Foldout>(k_CapturedObjectsFoldoutXmlName);
            m_CapturedObjectsFoldout.RegisterValueChangedCallback(val =>
                m_CapturedObjectsResourceItemsAreaVisualElement.style.display = val.newValue ? DisplayStyle.Flex : DisplayStyle.None);

            m_InfoLabel = m_RootVisualElement.Q<Label>(k_InfoLabelXmlName);

            ClearResourceItemsArea();

            m_SyncAssetBundlesButton.SetEnabled(false);
        }

        internal static bool IsPlatformInstalled(RuntimePlatform target)
        {
            switch (target)
            {
                case RuntimePlatform.IPhonePlayer:
                    return s_IsIosSupported;
                case  RuntimePlatform.Android:
                    return s_IsAndroidSupported;
                default:
                    return false;
            }
        }

        void OnEnable()
        {
            SetupUI();
            EditorApplication.update += EditorUpdate;

            CompanionResourceUtils.CloudResourceListChanged += SetAndSortResourceList;

#if INCLUDE_MARS
            (m_MarkerLibraries, m_MarkerLibraryNames) = CompanionImportUtils.RefreshMarkerLibraryList();
#endif
        }

        void OnDisable()
        {
            // ReSharper disable once DelegateSubtraction
            EditorApplication.update -= EditorUpdate;

            CompanionResourceUtils.CloudResourceListChanged -= SetAndSortResourceList;
            PlayerPrefs.Save();
        }

        #if INCLUDE_MARS
        void OnImageMarkerImGUI()
        {
            DrawMarkerLibraryDropdown(m_MarkerLibraryNames);
        }

        void DrawMarkerLibraryDropdown(string[] options)
        {
            // Check for deleted libraries
            foreach (var library in m_MarkerLibraries)
            {
                if (library == null)
                {
                    (m_MarkerLibraries, m_MarkerLibraryNames) = CompanionImportUtils.RefreshMarkerLibraryList();
                    break;
                }
            }

            using (var changed = new EditorGUI.ChangeCheckScope())
            using (new GUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(k_SelectMarkerLibraryContent, k_MarkerLibraryLabelWidth);

                var companionResourceSync = CompanionResourceSync.instance;
                var selectedIndex = 0;
                var length = m_MarkerLibraries.Length;
                for(; selectedIndex < length; selectedIndex++)
                {
                    if (m_MarkerLibraries[selectedIndex] == companionResourceSync.DefaultMarkerLibrary)
                        break;
                }

                selectedIndex = EditorGUILayout.Popup(selectedIndex, options);

                if (changed.changed)
                {
                    if (m_MarkerLibraries.Length == 0)
                    {
                        var newLibrary = CompanionEditorAssetUtils.CreateAndSaveNewLibrary();

                        (m_MarkerLibraries, m_MarkerLibraryNames) = CompanionImportUtils.RefreshMarkerLibraryList();

                        companionResourceSync.DefaultMarkerLibrary = newLibrary;
                    }
                    else
                    {
                        companionResourceSync.DefaultMarkerLibrary = m_MarkerLibraries[selectedIndex];
                    }
                }
            }

            EditorGUILayout.Space();
        }
#endif

        void EditorUpdate()
        {
            var projectId = Application.cloudProjectId;

            // Update project ID in case it has changed since the window opened
            m_CompanionProject.projectId = projectId;
            var isCloudProject = !string.IsNullOrEmpty(projectId);
            var moduleLoaderCore = ModuleLoaderCore.instance;
            var storageModule = moduleLoaderCore.GetModule<GenesisCloudStorageModule>();
            var isSignedIn = storageModule != null && storageModule.IdentityProvider != null && storageModule.IdentityProvider.IsValid;

            var isConnected = isCloudProject && isSignedIn;
            if (m_Setup)
            {
                this.SetProjectIdentifier(projectId);
            }
            else
            {
                var fiModule = moduleLoaderCore.GetModule<FunctionalityInjectionModule>();
                if (fiModule != null)
                {
                    fiModule.activeIsland.InjectFunctionalitySingle(this);
                    this.SetProjectIdentifier(projectId);
                    if (isConnected)
                    {
                        GetResourceList(m_CompanionProject);
                        m_Setup = true;
                    }
                }
            }

            m_RootVisualElement.SetEnabled(isConnected && !m_UpdatingResourceList);
            m_UnlinkedProjectMessageLabel.style.display = isCloudProject ? DisplayStyle.None : DisplayStyle.Flex;
            m_NotSignedInMessageLabel.style.display = isSignedIn ? DisplayStyle.None : DisplayStyle.Flex;

            var lastSyncTimeText = CompanionResourceUtils.GetRelativeTime(m_LastSync).ToLower();
            m_LastSyncedLabel.text = isConnected ? string.Format(k_LastUpdatedFormat, lastSyncTimeText) : string.Empty;

            UpdateScrollbarSpacer();

            m_NotSupportedBuildTargetVisualElement.style.display =
                CompanionImportUtils.IsCurrentBuildPlatformSupported() ? DisplayStyle.None : DisplayStyle.Flex;

            foreach (var state in m_ResourceSyncStates)
            {
                if (state.Value.Downloading)
                {
                    Repaint();
                    break;
                }
            }
        }

        void UpdateScrollbarSpacer() { m_ScrollbarSpacer.style.display = m_MainScrollView.verticalScroller.enabledSelf ? DisplayStyle.Flex : DisplayStyle.None; }

        void UpdateSyncAssetBundleState()
        {
            var needsUpdate = false;
            foreach (var kvp in m_AssetBundleButtons)
            {
                foreach (var button in kvp.Value)
                {
                    if (button.NeedsUpdate())
                    {
                        needsUpdate = true;
                        break;
                    }
                }
            }

            m_SyncAssetBundlesButton.SetEnabled(needsUpdate);
        }

        void OnReloadProjectButtonClick()
        {
            if(m_CompanionProject != null)
                GetResourceList(m_CompanionProject);
        }

        void OnSyncAllButtonClick()
        {
            foreach (var resource in m_SortedResourceList)
            {
                var state = GetOrCreateResourceSyncState(resource.key);
                if (state.Downloading)
                    continue;

                OnResourceSyncStart(resource, state);
                var currentPlatform = CompanionAssetUtils.GetCurrentBuildTargetAsRuntimePlatform();
                this.ImportResource(m_CompanionProject, resource, currentPlatform.ToString(), m_ResourceList,
                    m_SimulatedPlanePrefab, m_SimulatedPlaneMaterial, m_PostPrefab, m_MeshPrefab,
                    m_SimulatedLightingPrefab, m_LagCompensation, state, newAsset =>
                    {
                        OnResourceSyncEnd(resource, state);
                    },
                    (upload, download) =>
                    {
                        state.DownloadProgress = download;
                    });
            }
        }

        void OnResourceSyncStart(CompanionResource resource, ResourceSyncState state)
        {
            state.Downloading = true;
            if (m_ResourceButtons.TryGetValue(resource.key, out var button))
                button.text = k_CancelButtonText;
        }

        void OnResourceSyncEnd(CompanionResource resource, ResourceSyncState state)
        {
            state.Downloading = false;
            state.DownloadProgress = 0;
            if (m_ResourceButtons.TryGetValue(resource.key, out var button))
                button.text = GetSyncAction(resource);
        }

        void OnSyncAssetBundlesButtonClick()
        {
            m_BuildDictionary.Clear();
            m_UpdatingResourceList = true;

            // Get a fresh copy of the resource list in case new cloud resources have been added
            var resourceFolder = CompanionResourceUtils.GetCurrentResourceFolder();
            this.GetCloudResourceList(m_CompanionProject, resourceFolder, true, DoAssetBundleSync);
        }

        void FinalizeAssetBundleSync()
        {
            foreach (var kvp in m_Importers)
            {
                kvp.Value.SetAssetBundleNameAndVariant(string.Empty, string.Empty);
                AssetDatabase.RemoveAssetBundleName(kvp.Key, false);
            }

            m_Importers.Clear();
        }

        void DoAssetBundleSync(bool success, ResourceList resourceList)
        {
            if (!success)
            {
                Debug.LogError("Failed to get cloud resource list");
                return;
            }

            m_Importers.Clear();
            var resourceFolder = CompanionResourceUtils.GetCurrentResourceFolder();
            foreach (var platform in k_SupportedPlatforms)
            {
                if (!m_AssetBundleButtons.TryGetValue(platform, out var buttons))
                {
                    Debug.LogError("No buttons for supported platform " + platform);
                    continue;
                }

                if (!m_BuildDictionary.TryGetValue(platform, out var builds))
                {
                    builds = new List<AssetBundleBuild>();
                    m_BuildDictionary[platform] = builds;
                }

                foreach (var button in buttons)
                {
                    button.Process(resourceList, builds, m_Importers);
                }
            }

            this.WriteCloudResourceList(m_CompanionProject, resourceFolder, resourceList, writeSuccess =>
            {
                m_UpdatingResourceList = false;
                if (!writeSuccess)
                {
                    FinalizeAssetBundleSync();
                    Debug.LogError("Failed to write resource list");
                    return;
                }

                var shouldBuild = false;
                foreach (var kvp in m_BuildDictionary)
                {
                    if (kvp.Value.Count > 0)
                    {
                        shouldBuild = true;
                        break;
                    }
                }

                if (!shouldBuild)
                {
                    FinalizeAssetBundleSync();
                    SetAndSortResourceList(resourceList);
                    return;
                }

                BuildAssetBundles();
            });
        }

        void BuildAssetBundles()
        {
            if (EditorUtility.DisplayDialog(k_AssetBundleSyncTitle, k_AssetBundleSyncMessage,
                k_OkButtonTextAssetBundleSync, k_CancelButtonTextAssetBundleSync))
            {
                var resourceFolder = CompanionResourceUtils.GetCurrentResourceFolder();
                var supportedPlatforms = k_SupportedPlatforms.ToList();
                var currentTargetPlatform = GetCorrespondingRuntimePlatform(EditorUserBuildSettings.activeBuildTarget);

                // Make current target set by the editor the 1rst platform to check for exporting asset bundles to save time.
                if (currentTargetPlatform != null && supportedPlatforms.Contains(currentTargetPlatform.Value))
                {
                    supportedPlatforms.Remove(currentTargetPlatform.Value);
                    supportedPlatforms.Insert(0, currentTargetPlatform.Value);
                }

                m_UploadProgress.Clear();
                m_ResourceListUpdates.Clear();

                foreach (var platform in supportedPlatforms)
                {
                    if (!m_BuildDictionary.TryGetValue(platform, out var builds))
                        continue;

                    if (builds.Count == 0)
                        continue;

                    if (platform != currentTargetPlatform)
                    {
                        var switched = SwitchActiveBuildTarget(platform);

                        if (!switched)
                        {
                            Debug.LogWarning($"Cannot export AssetBundles. Failed to switch build platform to {platform}.");
                            continue;
                        }

                        currentTargetPlatform = platform;
                    }

                    CompanionEditorAssetUtils.BuildAssetBundles(builds.ToArray());
                    if (m_AssetBundleButtons.TryGetValue(platform, out var buttons))
                    {
                        foreach (var button in buttons)
                        {
                            if (button.PublishBundleAndCleanUp(resourceFolder, OnUploadCompleted, OnUploadProgress))
                            {
                                m_UploadCount++;
                                m_UploadProgress[button.Guid] = 0;
                            }
                        }
                    }
                }
            }

            FinalizeAssetBundleSync();
        }

        void OnUploadCompleted(bool success, string resourceKey, RuntimePlatform platform, string bundleKey, long fileSize)
        {
            if (success)
            {
                m_ResourceListUpdates.Add(new ResourceListUpdate
                {
                    ResourceKey = resourceKey,
                    Platform = platform.ToString(),
                    BundleKey = bundleKey,
                    FileSize = fileSize
                });
            }

            if (--m_UploadCount == 0)
            {
                m_InfoLabel.text = " ";
                var resourceFolder = CompanionResourceUtils.GetCurrentResourceFolder();
                this.GetCloudResourceList(m_CompanionProject, resourceFolder, true, SyncResourceListPostUpload);
            }
        }

        void SyncResourceListPostUpload(bool success, ResourceList resourceList)
        {
            if (!success)
            {
                Debug.LogError("Failed to get resource list after uploading bundles");
                return;
            }

            foreach (var update in m_ResourceListUpdates)
            {
                resourceList.AddOrUpdateBundle(update.ResourceKey, update.Platform, update.BundleKey, update.FileSize);
            }

            var resourceFolder = CompanionResourceUtils.GetCurrentResourceFolder();
            this.WriteCloudResourceList(m_CompanionProject, resourceFolder, resourceList, writeSuccess =>
            {
                if (!writeSuccess)
                {
                    Debug.LogError("Failed to write resource list");
                    return;
                }

                SetAndSortResourceList(resourceList);
            });
        }

        void OnUploadProgress(string guid, float progress)
        {
            m_UploadProgress[guid] = progress;

            var totalProgress = 0f;
            foreach (var kvp in m_UploadProgress)
            {
                totalProgress += kvp.Value;
            }

            totalProgress /= m_UploadProgress.Count;
            m_InfoLabel.text = string.Format(k_ProgressLabelFormat, totalProgress * 100);
        }

        void OnSelectAllPlatformButtonClick(RuntimePlatform platform, ref bool select)
        {
            select = !select;

            if (!m_AssetBundleButtons.TryGetValue(platform, out var buttons))
                return;

            if (select)
            {
                foreach (var button in buttons)
                {
                    if (!button.enabledSelf)
                        continue;

                    button.SetUploadOrUpdate();
                }
            }
            else
            {
                foreach (var button in buttons)
                {
                    if (!button.enabledSelf)
                        continue;

                    button.SetNotUploaded();
                }
            }

            UpdateSyncAssetBundleState();
        }

        internal void Refresh()
        {
            GetResourceList(m_CompanionProject);
        }

        void GetResourceList(CompanionProject project)
        {
            m_InfoLabel.text = k_RefreshingListText;
            Repaint();
            m_LastSync = DateTime.UtcNow;
            var resourceFolder = CompanionResourceUtils.GetCurrentResourceFolder();
            this.GetCloudResourceList(project, resourceFolder, true, (success, resourceList) =>
            {
                SetAndSortResourceList(resourceList);
            });
        }

        void SetAndSortResourceList(ResourceList resourceList)
        {
            // Clear cached asset packs in case scenes were removed and are added back
            AssetPack.ClearSceneToAssetPackCache();

            // In edit mode, make sure all resources are cloud resources
            if (!Application.isPlaying)
                resourceList.SetCloudResource(true);

            m_SortedResourceList = resourceList.GetSortedList();
            m_ResourceList = resourceList;
            FillResourceItemsVisualElements(m_SortedResourceList);
            UpdateSyncAssetBundleState();
            UpdateScrollbarSpacer();
            m_InfoLabel.text = " ";
            Repaint();
        }

        void FillResourceItemsVisualElements(List<CompanionResource> sortedResourceList)
        {
            ClearResourceItemsArea();

            var currentPlatform = CompanionAssetUtils.GetCurrentBuildTargetAsRuntimePlatform().ToString();
            var resourceSync = CompanionResourceSync.instance;
            var isProSkin = EditorGUIUtility.isProSkin;
            foreach (var resource in sortedResourceList)
            {
                UnityObject asset = null;
                string cloudGuid = null;
                string assetGuid;
                string assetPath;

                var foldout = GetFoldoutByType(resource.type);
                if (foldout == null)
                    continue;

                foldout.SetEnabled(true);

                var resourceItemAreaVisualElement = GetResourceItemAreaByType(resource.type);
                resourceItemAreaVisualElement.SetEnabled(true);
                resourceItemAreaVisualElement.Add(m_ResourceItemVisualTreeAsset.CloneTree());

                var currResourceItemVisualElement = resourceItemAreaVisualElement[resourceItemAreaVisualElement.childCount - 1];
                if ((resourceItemAreaVisualElement.childCount - 1) % 2 != 0)
                    currResourceItemVisualElement.style.backgroundColor = isProSkin ? k_DarkColor : k_LightColor;

                var isManagementSupported = IsResourceManagementSupported(resource.type);

                // Resource ObjectField
                var currObjField = currResourceItemVisualElement.Q<ObjectField>(k_AssetReferenceObjectFieldXmlName);
                currObjField.allowSceneObjects = false;
                currObjField.SetEnabled(isManagementSupported);

                // Resource SyncStatus
                var key = resource.key;
                var state = GetOrCreateResourceSyncState(key);

                var syncButton = currResourceItemVisualElement.Q<Button>(k_SyncButtonXmlName);
                var syncStatus = resource.GetSyncStatus();
                syncButton.SetEnabled(isManagementSupported && CanSync(syncStatus));
                syncButton.text = state.Downloading ? k_CancelButtonText : GetSyncAction(resource);

                m_ResourceButtons[key] = syncButton;

                // Resource Download Progress
                var progressBarGUIContainer = currResourceItemVisualElement.Q<IMGUIContainer>(k_ProgressBarXmlName);
                progressBarGUIContainer.onGUIHandler += () =>
                {
                    var progress = state.DownloadProgress;
                    if (progress > 0)
                        EditorGUI.ProgressBar(new Rect(0, 4, k_ProgressBarWidth, 16), progress, string.Empty);
                };

                syncButton.clicked += () =>
                {
                    var requests = state.GetOrCreateRequestStack();

                    // Sync button cancels requests during download
                    if (state.Downloading)
                    {
                        while (requests.Count > 0)
                        {
                            this.CancelRequest(requests.Pop());
                        }

                        OnResourceSyncEnd(resource, state);
                        Debug.Log("Resource Sync canceled. You can safely ignore the Curl error messages.");
                        return;
                    }

                    state.Downloading = true;
                    syncButton.text = k_CancelButtonText;

                    this.ImportResource(m_CompanionProject, resource, currentPlatform, m_ResourceList,
                        m_SimulatedPlanePrefab, m_SimulatedPlaneMaterial, m_PostPrefab,
                        m_MeshPrefab, m_SimulatedLightingPrefab, m_LagCompensation, state, newAsset =>
                        {
                            if (newAsset != null)
                                currObjField.value = newAsset;

                            OnResourceSyncEnd(resource, state);
                        },
                        (upload, download) =>
                        {
                            state.DownloadProgress = download;
                        });
                };

                var resourceKey = resource.key;
                switch (resource.type)
                {
                    case ResourceType.Scene:
                        currObjField.objectType = typeof(SceneAsset);
                        CompanionSceneUtils.SplitSceneKey(resourceKey, out _, out cloudGuid);
                        if (string.IsNullOrEmpty(cloudGuid))
                            continue;

                        assetGuid = resourceSync.GetAssetGuid(cloudGuid);
                        if (!string.IsNullOrEmpty(assetGuid))
                        {
                            assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
                            if (!string.IsNullOrEmpty(assetPath))
                                asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(assetPath);
                        }

                        currObjField.value = asset;
                        currObjField.RegisterValueChangedCallback(value =>
                        {
                            var newScene = value.newValue;
                            var path = AssetDatabase.GetAssetPath(newScene);
                            if (!string.IsNullOrEmpty(path))
                            {
                                assetGuid = AssetDatabase.AssetPathToGUID(path);
                                resourceSync.SetAssetGuid(assetGuid, cloudGuid);
                            }

                            currObjField.value = newScene;
                        });

                        break;
                    case ResourceType.Prefab:
                        currObjField.objectType = typeof(GameObject);
                        CompanionAssetUtils.SplitPrefabKey(resourceKey, out _, out assetGuid);
                        if (string.IsNullOrEmpty(assetGuid))
                            continue;

                        assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
                        if (!string.IsNullOrEmpty(assetPath))
                            asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);

                        currObjField.value = asset;

                        // Always set the value back to the current asset -- field is read-only
                        currObjField.RegisterValueChangedCallback(change => currObjField.value = asset);

                        // If there is no asset bundle then you can't preview the prefab
                        if (!m_ResourceList.TryGetBundleGroup(resourceKey, out var bundleGroup) || !bundleGroup.HasBundleForPlatform(currentPlatform))
                        {
                            syncButton.SetEnabled(false);
                            syncButton.tooltip = k_PrefabTestDisableReason;
                        }

                        break;
                    case ResourceType.Marker:
                        currObjField.objectType = typeof(GameObject);
                        CompanionMarkerUtils.SplitMarkerKey(resourceKey, out _, out cloudGuid);

                        if (string.IsNullOrEmpty(cloudGuid))
                            continue;

                        assetGuid = resourceSync.GetAssetGuid(cloudGuid);
                        if (!string.IsNullOrEmpty(assetGuid))
                        {
                            assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
                            if (!string.IsNullOrEmpty(assetPath))
                                asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                        }

                        currObjField.value = asset;
                        currObjField.RegisterValueChangedCallback(value =>
                        {
                            var newAsset = value.newValue;
                            var path = AssetDatabase.GetAssetPath(newAsset);
                            if (!string.IsNullOrEmpty(path))
                            {
                                assetGuid = AssetDatabase.AssetPathToGUID(path);
                                resourceSync.SetAssetGuid(assetGuid, cloudGuid);
                            }

                            currObjField.value = newAsset;
                        });

                        break;
                    case ResourceType.Environment:
                        currObjField.objectType = typeof(GameObject);
                        CompanionEnvironmentUtils.SplitEnvironmentKey(resourceKey, out _, out cloudGuid);
                        if (string.IsNullOrEmpty(cloudGuid))
                            continue;

                        assetGuid = resourceSync.GetAssetGuid(cloudGuid);
                        if (!string.IsNullOrEmpty(assetGuid))
                        {
                            assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
                            if (!string.IsNullOrEmpty(assetPath))
                                asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                        }

                        currObjField.value = asset;
                        currObjField.RegisterValueChangedCallback(value =>
                        {
                            var newAsset = value.newValue;
                            var path = AssetDatabase.GetAssetPath(newAsset);
                            if (!string.IsNullOrEmpty(path))
                            {
                                assetGuid = AssetDatabase.AssetPathToGUID(path);
                                resourceSync.SetAssetGuid(assetGuid, cloudGuid);
                            }

                            currObjField.value = newAsset;
                        });

                        break;
                    case ResourceType.Recording:
                        currObjField.objectType = typeof(TimelineAsset);
                        CompanionDataRecordingUtils.SplitDataRecordingKey(resourceKey, out _, out cloudGuid);
                        if (string.IsNullOrEmpty(cloudGuid))
                            continue;

                        assetGuid = resourceSync.GetAssetGuid(cloudGuid);
                        if (!string.IsNullOrEmpty(assetGuid))
                        {
                            assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
                            if (!string.IsNullOrEmpty(assetPath))
                                asset = AssetDatabase.LoadAssetAtPath<TimelineAsset>(assetPath);
                        }

                        currObjField.value = asset;
                        currObjField.RegisterValueChangedCallback(value =>
                        {
                            var newAsset = value.newValue;
                            var path = AssetDatabase.GetAssetPath(newAsset);
                            if (!string.IsNullOrEmpty(path))
                            {
                                assetGuid = AssetDatabase.AssetPathToGUID(path);
                                resourceSync.SetAssetGuid(assetGuid, cloudGuid);
                            }

                            currObjField.value = newAsset;
                        });
                        break;
                    case ResourceType.CapturedObject:
                        currObjField.objectType = typeof(GameObject);
                        currObjField.value = null;
                        CompanionObjectCaptureUtils.SplitCapturedObjectKey(resourceKey, out _, out cloudGuid);

                        syncButton.text = k_CapturedObjectSyncAction;
                        break;
                }

                // Resource name
                var resourceNameLabel = currResourceItemVisualElement.Q<Label>(k_AssetNameLabelXmlName);
                resourceNameLabel.text = resource.name;
                resourceNameLabel.tooltip = resourceKey;

                // Resource Size
                var extraSize = m_ResourceList.GetBundleGroupSize(resourceKey, true);
                var resourceSizeLabel = currResourceItemVisualElement.Q<Label>(k_SizeLabelLabelXmlName);
                resourceSizeLabel.text = resource.GetReadableFileSize(extraSize);
                resourceSizeLabel.tooltip = resourceKey;

                // Resource Date
                var resourceDateLabel = currResourceItemVisualElement.Q<Label>(k_DateLabelXmlName);
                resourceDateLabel.text = resource.GetReadableTimestamp();
                resourceDateLabel.tooltip = resourceKey;

                // Resource Platform Toggles
                SetUpAssetBundleButtons(resource, currResourceItemVisualElement, asset,
                    isManagementSupported);

                // Resource Delete
                currResourceItemVisualElement.Q<Button>(k_DeleteCompanionResourceButtonXmlName).clicked += () =>
                {
                    OnDeleteResourceItem(asset, cloudGuid, resource, resourceSync);
                };
            }
        }

        ResourceSyncState GetOrCreateResourceSyncState(string key)
        {
            if (!m_ResourceSyncStates.TryGetValue(key, out var state))
            {
                state = new ResourceSyncState();
                m_ResourceSyncStates[key] = state;
            }

            return state;
        }

        static bool CanSync(SynchronizationStatus status)
        {
            switch (status)
            {
                case SynchronizationStatus.CloudOnly:
                case SynchronizationStatus.Conflicted:
                case SynchronizationStatus.None:
                    return true;
                default:
                    return false;
            }
        }

        static string GetSyncAction(CompanionResource resource)
        {
            var status = resource.GetSyncStatus();
            switch (status)
            {
                case SynchronizationStatus.CloudOnly:
                case SynchronizationStatus.None:
                    switch (resource.type)
                    {
                        // Because the scene is not saved immediately, we want a slightly different term here
                        case ResourceType.Scene:
                            return k_SceneSyncAction;

                        // Because the prefab cannot be imported, we want a slightly different term here
                        case ResourceType.Prefab:
                            return k_PrefabSyncAction;

                        default:
                            return "Download";
                    }

                case SynchronizationStatus.Conflicted:
                    return "Resolve";
                default:
                    return "Synced";
            }
        }

        void SetUpAssetBundleButtons(CompanionResource resource, VisualElement resourceItem, UnityObject asset, bool isManagementSupported)
        {
            var container = resourceItem.Q<VisualElement>(k_CompanionResourceItemXmlName);
            var resourceType = resource.type;

            // Insert buttons to the left of the last visual element, the delete button
            var insertionIndex = container.childCount - 1;
            if (!resource.hasAssetBundle || !(resourceType == ResourceType.Scene || resourceType == ResourceType.Prefab))
            {
                for (var i = 0; i < k_SupportedPlatforms.Length; i++)
                {
                    var spacer = new VisualElement();
                    spacer.AddToClassList(ColumnBundleClassName);
                    container.Insert(insertionIndex + i, spacer);
                }

                return;
            }

            m_ResourceList.TryGetBundleGroup(resource.key, out var bundleGroup);

            if (asset is SceneAsset sceneAsset)
                asset = AssetPack.GetAssetPackForScene(sceneAsset);

            var count = 0;
            foreach (var platform in k_SupportedPlatforms)
            {
                if (!m_AssetBundleButtons.TryGetValue(platform, out var buttons))
                {
                    buttons = new List<AssetBundleButton>();
                    m_AssetBundleButtons[platform] = buttons;
                }

                AssetBundleResource bundle = null;
                bundleGroup?.TryGetBundleResource(platform.ToString(), out bundle);
                var button = new AssetBundleButton(resource, bundle, platform, asset, this);
                container.Insert(insertionIndex + count++, button);
                button.clicked += UpdateSyncAssetBundleState;
                buttons.Add(button);
                button.SetEnabled(button.enabledSelf && isManagementSupported);
            }
        }

        void OnDeleteResourceItem(UnityObject asset, string cloudGuid, CompanionResource resource, CompanionResourceSync resourceSync)
        {
            var resourceFolder = CompanionResourceUtils.GetCurrentResourceFolder();
            m_UpdatingResourceList = true;

            var state = GetOrCreateResourceSyncState(resource.key);
            var requests = state.GetOrCreateRequestStack();
            this.DeleteResource(m_CompanionProject, resourceFolder, resource, requests, (success, resourceList) =>
            {
                m_UpdatingResourceList = false;
                if (success)
                {
                    if (resource.type == ResourceType.Scene)
                        AssetPack.RemoveCachedAssetPack(asset as SceneAsset);

                    if (resource.type != ResourceType.Prefab && !string.IsNullOrEmpty(cloudGuid))
                        resourceSync.RemoveCloudGuid(cloudGuid);
                }
            });
        }

        // ReSharper disable once UnusedParameter.Local
        static bool IsResourceManagementSupported(ResourceType type)
        {
#if INCLUDE_MARS
            return true;
#else
            switch (type)
            {
                case ResourceType.Environment:
                    return false;
                case ResourceType.Marker:
                    return false;
                case ResourceType.Recording:
                    return false;
                default:
                    return true;
            }
#endif
        }

        VisualElement GetResourceItemAreaByType(ResourceType type)
        {
            switch (type)
            {
                case ResourceType.Environment:
                    return m_EnvironmentsResourceItemsAreaVisualElement;
                case ResourceType.Marker:
                    return m_MarkersResourceItemsAreaVisualElement;
                case ResourceType.Prefab:
                    return m_PrefabsResourceItemsAreaVisualElement;
                case ResourceType.Recording:
                    return m_RecordingsResourceItemsAreaVisualElement;
                case ResourceType.Scene:
                    return m_ScenesResourceItemsAreaVisualElement;
                case ResourceType.CapturedObject:
                    return m_CapturedObjectsResourceItemsAreaVisualElement;
                default:
                    Debug.LogError("Unknown resource type: " + type);
                    return null;
            }
        }

        Foldout GetFoldoutByType(ResourceType type)
        {
            switch (type)
            {
                case ResourceType.Environment:
                    return m_EnvironmentsFoldout;
                case ResourceType.Marker:
                    return m_MarkersFoldout;
                case ResourceType.Prefab:
                    return m_PrefabsFoldout;
                case ResourceType.Recording:
                    return m_RecordingsFoldout;
                case ResourceType.Scene:
                    return m_ScenesFoldout;
                case ResourceType.CapturedObject:
                    return m_CapturedObjectsFoldout;
                default:
                    Debug.LogError($"Unknown resource type: {type}. You may need to update the AR Companion Core package.");
                    return null;
            }
        }

        void ClearResourceItemsArea()
        {
            m_AssetBundleButtons.Clear();
            m_ResourceButtons.Clear();

            m_EnvironmentsFoldout.SetEnabled(false);
            m_EnvironmentsResourceItemsAreaVisualElement.Clear();
            m_MarkersFoldout.SetEnabled(false);
            m_MarkersResourceItemsAreaVisualElement.Clear();
            m_PrefabsFoldout.SetEnabled(false);
            m_PrefabsResourceItemsAreaVisualElement.Clear();
            m_RecordingsFoldout.SetEnabled(false);
            m_RecordingsResourceItemsAreaVisualElement.Clear();
            m_ScenesFoldout.SetEnabled(false);
            m_ScenesResourceItemsAreaVisualElement.Clear();
            m_CapturedObjectsFoldout.SetEnabled(false);
            m_CapturedObjectsResourceItemsAreaVisualElement.Clear();
        }

        static bool SwitchActiveBuildTarget(RuntimePlatform platform)
        {
            switch (platform)
            {
                case RuntimePlatform.IPhonePlayer:
                    return EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS);
                case RuntimePlatform.Android:
                    return EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
                case RuntimePlatform.Lumin:
                    return EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Lumin, BuildTarget.Lumin);
                default:
                    throw new ArgumentException($"Cannot get target and target group for platform {platform}.");
            }
        }

        static RuntimePlatform? GetCorrespondingRuntimePlatform(BuildTarget target)
        {
            switch (target)
            {
                case BuildTarget.iOS:
                    return RuntimePlatform.IPhonePlayer;
                case BuildTarget.Android:
                    return RuntimePlatform.Android;
                case BuildTarget.Lumin:
                    return RuntimePlatform.Lumin;
                default:
                    return null;
            }
        }
    }
}
