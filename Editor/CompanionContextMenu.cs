using System.Collections.Generic;
using Unity.RuntimeSceneSerialization;
using Unity.XRTools.ModuleLoader;
using Unity.XRTools.Utils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityObject = UnityEngine.Object;
using Unity.AR.Companion.CloudStorage;

#if INCLUDE_MARS
using Unity.MARS.MARSUtils;
using UnityEditor.MARS.Simulation;
#endif

namespace Unity.AR.Companion.Core
{
    static class CompanionContextMenu
    {
        class Subscriber : IUsesCloudStorage
        {
            public IProvidesCloudStorage provider { get; set; }
        }

        struct ResourceListUpdate
        {
            public string ResourceKey;
            public string Name;
            public ResourceType Type;
            public long FileSize;
            public bool HasBundle;
            public byte[] Thumbnail;
        }

        const float k_MinPrefabBound = 0.001f;
        static readonly List<ResourceListUpdate> k_ResourceListUpdates = new List<ResourceListUpdate>();
        static int s_UploadCount;

        static readonly Stack<RequestHandle> k_Requests = new Stack<RequestHandle>();

        static Subscriber CreateSubscriber()
        {
            var fiModule = ModuleLoaderCore.instance.GetModule<FunctionalityInjectionModule>();
            if (!fiModule)
            {
                GUILayout.Label("Modules not loaded");
                return null;
            }

            var subscriber = new Subscriber();
            fiModule.activeIsland.InjectFunctionalitySingle(subscriber);
            return subscriber;
        }

#if INCLUDE_MARS
        static byte[] SimulateSceneAndCaptureThumbnail()
        {
            var moduleLoaderCore = ModuleLoaderCore.instance;
            var environmentManager = moduleLoaderCore.GetModule<MARSEnvironmentManager>();
            var simModule = moduleLoaderCore.GetModule<QuerySimulationModule>();

            var miniSimView = ScriptableObject.CreateInstance<MiniSimulationView>();
            var simSceneModule = moduleLoaderCore.GetModule<SimulationSceneModule>();
            simSceneModule.RegisterSimulationUser(miniSimView);

            const int size = CompanionResourceUtils.ThumbnailSize;
            miniSimView.InitMiniSimulationView(size, size);

            environmentManager.TryFrameAllSimViewsOnEnvironment();

            if (!environmentManager.EnvironmentSetup)
                environmentManager.TrySetupNextEnvironmentAndRestartSimulation(true);

            simModule.RestartSimulationIfNeeded(true);
            moduleLoaderCore.GetModule<SimulatedObjectsManager>().DirtySimulatableScene();

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            TextureUtils.RenderTextureToTexture2D(miniSimView.SingleFrameRepaint(true), texture);
            RenderTexture.active = null;

            var thumbnail = texture.EncodeToPNG();
            UnityObject.DestroyImmediate(texture);
            simSceneModule.UnregisterSimulationUser(miniSimView);
            UnityObject.DestroyImmediate(miniSimView);
            return thumbnail;
        }
#endif

        [MenuItem("Assets/AR Companion/Publish", false)]
        static void Publish()
        {
            k_ResourceListUpdates.Clear();
            s_UploadCount = 0;
            var subscriber = CreateSubscriber();
            var resourceFolder = CompanionResourceUtils.GetCurrentResourceFolder();
            var project = new CompanionProject(Application.cloudProjectId, Application.productName, true);
            foreach (var selected in Selection.objects)
            {
                if (selected is SceneAsset sceneAsset)
                {
                    var path = AssetDatabase.GetAssetPath(sceneAsset);
                    if (string.IsNullOrEmpty(path))
                    {
                        Debug.LogError("Selected SceneAsset path is invalid");
                        continue;
                    }

                    var sceneAssetPack = CompanionSceneUtils.GetOrTryCreateAssetPackForSceneAsset(sceneAsset);
                    sceneAssetPack.Clear();
                    sceneAssetPack.SceneAsset = sceneAsset;
                    var removeScene = true;
                    var unload = true;
                    var sceneCount = SceneManager.sceneCount;
                    for (var i = 0; i < sceneCount; i++)
                    {
                        var existingScene = SceneManager.GetSceneAt(i);
                        if (existingScene.path == path)
                        {
                            if (existingScene.isLoaded)
                                unload = false;

                            removeScene = false;
                            break;
                        }
                    }

                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

                    var previousActiveScene = SceneManager.GetActiveScene();
                    SceneManager.SetActiveScene(scene);
                    var renderSettings = SerializedRenderSettings.CreateFromActiveScene();
                    SceneManager.SetActiveScene(previousActiveScene);

                    var assetGuid = AssetDatabase.AssetPathToGUID(path);
                    if (string.IsNullOrEmpty(assetGuid))
                    {
                        Debug.LogError("Could not get valid guid for asset at path " + path);
                        continue;
                    }

                    // Hide the OnDestroyNotifier so it is not saved
                    foreach (var root in scene.GetRootGameObjects())
                    {
                        var notifier = root.GetComponentInChildren<OnDestroyNotifier>();
                        if (notifier != null)
                            notifier.gameObject.hideFlags = HideFlags.HideAndDontSave;
                    }

                    // generate and save thumbnail image for the scene by running a one-shot simulation
#if INCLUDE_MARS
                    var thumbnail = SimulateSceneAndCaptureThumbnail();
#else
                    var thumbnail = new Texture2D(1, 1).EncodeToPNG();
                    Debug.LogWarning("Unable to simulate scene for scene resource thumbnail!");
#endif
                    var cloudGuid = CompanionResourceSync.instance.GetCloudGuid(assetGuid);
                    s_UploadCount++;
                    k_Requests.Clear();
                    k_Requests.Push(subscriber.SaveScene(project, resourceFolder, cloudGuid, scene, renderSettings,
                        sceneAssetPack, (success, key, size) =>
                        {
                            if (scene.IsValid())
                            {
                                // Restore OnDestroyNotifier HideFlags so that it is discoverable
                                foreach (var root in scene.GetRootGameObjects())
                                {
                                    var notifier = root.GetComponentInChildren<OnDestroyNotifier>();
                                    if (notifier != null)
                                        notifier.gameObject.hideFlags = HideFlags.HideInHierarchy;
                                }
                            }

                            var sceneName = sceneAsset.name;
                            if (success)
                            {
                                k_ResourceListUpdates.Add(new ResourceListUpdate
                                {
                                    ResourceKey = key,
                                    Name = sceneName,
                                    Type = ResourceType.Scene,
                                    FileSize = size,
                                    HasBundle = sceneAssetPack.AssetCount > 0,
                                    Thumbnail = thumbnail
                                });

                                Debug.Log($"Published {sceneName}");
                            }
                            else
                            {
                                Debug.LogError($"Failed to publish {sceneName}");
                            }

                            if (--s_UploadCount == 0)
                                FinalizePublish(k_Requests);
                        }));

                    // Scene serialization is synchronous, so we can close the scene while the upload completes
                    if (unload)
                        EditorSceneManager.CloseScene(scene, removeScene);

                    continue;
                }

                if (selected is GameObject prefab)
                {
                    var path = AssetDatabase.GetAssetPath(prefab);
                    if (string.IsNullOrEmpty(path))
                    {
                        Debug.LogError("SceneAsset for selected AssetPack path is invalid");
                        continue;
                    }

                    var guid = AssetDatabase.AssetPathToGUID(path);
                    if (string.IsNullOrEmpty(guid))
                    {
                        Debug.LogError("Could not get valid guid for asset at path " + path);
                        continue;
                    }

                    // Check existing asset bundle name to avoid overwriting it
                    var assetImporter = AssetImporter.GetAtPath(path);
                    if (!string.IsNullOrEmpty(assetImporter.assetBundleName) && assetImporter.assetBundleName != guid)
                    {
                        Debug.LogError(prefab.name + " is already part of an AssetBundle, and cannot be published to the cloud without overwriting its AssetBundle name. You need to temporarily set its AssetBundle name to None in the inspector in order to publish this asset.");
                        continue;
                    }

                    var bounds = BoundsUtils.GetBounds(prefab.transform);
                    if (bounds.extents.MinComponent() < k_MinPrefabBound)
                    {
                        Debug.LogError(prefab.name + " is too small to be selectable. AR Companion prefabs must be selectable.");
                        continue;
                    }

                    var thumbnail = ThumbnailUtility.GeneratePrefabThumbnail(prefab);
                    if (thumbnail == null)
                    {
                        Debug.LogError(prefab.name + " is not a visible asset. AR Companion prefabs must be visible.");
                        continue;
                    }

                    s_UploadCount++;
                    k_Requests.Clear();
                    k_Requests.Push(subscriber.SavePrefab(project, resourceFolder, prefab, (success, key) =>
                    {
                        if (success)
                        {
                            k_ResourceListUpdates.Add(new ResourceListUpdate
                            {
                                ResourceKey = key,
                                Name = prefab.name,
                                Type = ResourceType.Prefab,
                                HasBundle = true,
                                Thumbnail = thumbnail
                            });

                            Debug.Log($"Published {prefab.name}");
                        }
                        else
                        {
                            Debug.LogError($"Failed to publish {prefab.name}");
                        }

                        if (--s_UploadCount == 0)
                            FinalizePublish(k_Requests);
                    }));
                }
            }
        }

        [MenuItem("Assets/AR Companion/Publish", true)]
        static bool ValidatePublish()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return false;

            if (s_UploadCount > 0)
                return false;

            var isValid = false;
            foreach (var selected in Selection.objects)
            {
                if (selected is SceneAsset || selected is GameObject)
                {
                    isValid = true;
                    break;
                }
            }

            return isValid;
        }

        static void FinalizePublish(Stack<RequestHandle> requests)
        {
            var subscriber = CreateSubscriber();
            var resourceFolder = CompanionResourceUtils.GetCurrentResourceFolder();
            var project = new CompanionProject(Application.cloudProjectId, Application.productName, true);
            requests.Push(subscriber.GetCloudResourceList(project, resourceFolder, true, (success, resourceList) =>
            {
                foreach (var update in k_ResourceListUpdates)
                {
                    var key = update.ResourceKey;
                    resourceList.AddOrUpdateResource(key, update.Name, update.Type, update.FileSize, update.HasBundle);

                    var thumbnail = update.Thumbnail;
                    if (thumbnail != null)
                        subscriber.SaveThumbnailImage(project, resourceFolder, key, thumbnail, success, requests);
                }

                requests.Push(subscriber.WriteCloudResourceList(project, resourceFolder, resourceList, writeSuccess =>
                {
                    if (success)
                        CompanionResourceUtils.OnCloudResourceListChanged(resourceList);
                    else
                        Debug.LogError("Failed to write resource list");
                }));
            }));
        }
    }
}
