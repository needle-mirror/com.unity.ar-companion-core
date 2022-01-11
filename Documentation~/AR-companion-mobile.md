# About Unity AR Companion Mobile

The Unity AR Companion Mobile app allows you to capture real-world data directly on a mobile device and bring it into the Unity Editor to help create and iterate on AR experiences.

The Unity AR Companion Mobile app provides five main workflows for AR capture:

- [Create Scene](scene-creation-workflow.md)
- [Capture Environment](environment-capture-workflow.md)
- [Capture Image Markers](marker-capture-workflow.md)
- [Record Data](record-data-workflow.md)
- [Capture Object](object-capture-workflow.md)

> [!TIP]
> You can upload Prefabs from a linked Unity Project to place in Scenes in the mobile app. You can also modify Scenes that you create with the mobile app in the Unity Editor and open them again in the mobile app. See [Publish Scenes and Prefabs](publish-scenes-prefabs.md) for more information.

## Linking your Unity account to the AR Companion Mobile app

The first time you run the app, you are prompted to sign in. In order to access cloud storage and sync assets with the Editor, you must sign in with a Unity account. To sign in, follow these steps:

1. Tap **Login**
2. You will be redirected to a browser where you can follow the normal Unity sign in process using your e-mail and password or your preferred authentication provider
3. If login is successful, you are returned to the app **Project List**

If you cannot connect to the internet, or if you just want to try the Unity AR Companion Mobile app without signing in, you can tap **Skip** to use the app in offline mode. You will not be able to link your project or access cloud storage for any existing linked projects, but you can create and manage resources in your device's local storage.

Your sign-in is valid for 30 days. During this time, the app automatically bypasses the **Sign In** view. After 30 days, simply tap **Login** and repeat the browser sign in process.

<a name="link-project"></a>
## Linking your project to the AR Companion Mobile app

From the **Project List** view, you can either create a new project, or, if you are signed in, import a project from the Editor.

To create a new project and start capturing data, tap **New Project**.

If you have a project open in the Editor, or a **Project ID**, tap **Import Project**, which opens the QR code reader.

In the Unity Editor, click the **QR Code** button in the [Companion Resources window](companion-resource-manager.md) to display the **Project ID QR Code**, then scan this code. When the scan is successful, the **Project ID** will display in the Unity Companion App, and the project title will replace the instructions. You can also manually enter the **Project ID** into the text field.

Tap **Link Project** to complete the linking process. This brings you to the [Resource list](companion-resource-view.md) view for the Project.

## AR capture modes

Tap the **AR Capture Mode** at the bottom of a Project's **Resource List** view to enter the [Home](companion-home-view.md) view, where you can switch between the app's five main modes:

- [Create Scene](scene-creation-workflow.md): Create, modify, and test Scenes.
- [Environment](environment-capture-workflow.md): Capture environmental information to use in the Unity MARS Simulation view.
- [Marker Creation](marker-capture-workflow.md): Create image markers.
- [Record Data](record-data-workflow.md): Record AR sessions to playback in the Unity MARS Simulation view.
- [Capture Object](object-capture-workflow.md): Scan objects by taking a set of photographs with the device camera.

# Technical details

The Unity AR Companion Mobile apps are built with Unity 2019.4.30f1.

## Requirements

The Unity AR Companion Mobile app for iOS requires Apple ARKit and iOS 11.0 or later.

The Unity AR Companion Mobile app for Android requires Google ARCore and Android 7.0 or later.

## Known limitations

Unity AR Companion Mobile app has the following known limitations:

* There is no limitation on the length of data recordings. Extremely long recordings might crash the app before they can be saved.
* When linking a mobile project to a Cloud project, the Cloud project name takes precedence.
* AssetBundles built with Unity versions later than 2019.4.30f1 (chronologically) may not import properly in the app. See [AssetBundle Compatibility](publish-scenes-prefabs.md#assetbundle-compatibility) for more information.

<hr>
\* *Apple and ARKit are trademarks of Apple Inc., registered in the U.S. and other countries and regions.*