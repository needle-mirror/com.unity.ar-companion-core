namespace Unity.AR.Companion.Core
{
    /// <summary>
    /// Issue codes for the core package
    /// </summary>
    public static class CoreIssueCodes
    {
        /// <summary>
        /// Issue code for user not signed in
        /// </summary>
        public const string UserNotSignedIn = "user-not-signed-in";

        /// <summary>
        /// Issue code for network unreachable
        /// </summary>
        public const string NetworkUnreachable = "network-unreachable";

        internal const string CompanionFileRead = "companion-file-read";

        internal const string CompanionFileWrite = "companion-file-write";

        internal const string CompanionFileMove = "companion-file-move";

        internal const string CompanionFileDelete = "companion-file-delete";

        internal const string CompanionFileMissing = "companion-file-missing";

        internal const string CompanionUploadFailed = "companion-upload-failed";

        internal const string CompanionDownloadFailed = "companion-download-failed";

        internal const string CompanionSceneDownloadFailed = "companion-scene-download-failed";

        internal const string CompanionUnknownResourceUpload = "companion-unknown-resource-upload";

        internal const string CompanionUnknownResourceDownload = "companion-unknown-resource-download";

        internal const string CompanionUnknownResourceDelete = "companion-unknown-resource-delete";

        internal const string CompanionAssetLoadError = "companion-asset-load-error";
    }
}
