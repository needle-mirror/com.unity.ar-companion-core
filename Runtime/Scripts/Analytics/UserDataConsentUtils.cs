#if UNITY_IOS && !UNITY_EDITOR
#define UNITY_IOS_PLAYER
#endif

using System;
using UnityEngine;

#if UNITY_IOS_PLAYER
using System.Runtime.InteropServices;
#endif

namespace Unity.AR.Companion.Core
{
    static class AppTrackingConsentCodes
    {
        public const string CompanionDataConsent = "companion-data-consent";
        public const string CompanionDataExportConsent = "companion-data-export-consent";
    }

    static class UserDataConsentUtils
    {
        const string k_DataConsentStatusPrefsKey = "AR.Companion.DataConsentStatus";
        const string k_DataExportConsentStatusPrefsKey = "AR.Companion.DataExportConsentStatus";

        static bool s_HasProvidedExportConsent;

#if !UNITY_IOS_PLAYER
        static bool s_HasProvidedTrackingConsent;
#endif

#if UNITY_IOS_PLAYER && UNITY_AR_COMPANION_APP_TRACKING
        [DllImport("__Internal")]
        static extern void RequestAppTrackingAuthorization();

        [DllImport("__Internal")]
        static extern int GetAppTrackingTransparencyStatus();
#endif

        public static void RequestAppTrackingConsent()
        {
#if UNITY_IOS_PLAYER && UNITY_AR_COMPANION_APP_TRACKING
            RequestAppTrackingAuthorization();
#elif !UNITY_IOS_PLAYER && UNITY_AR_COMPANION_APP_TRACKING
            if (PlayerPrefs.HasKey(k_DataConsentStatusPrefsKey))
            {
                s_HasProvidedTrackingConsent = true;
            }
            else
            {
                //Show Consent Dialog
                CompanionIssueUtils.HandleIssue(AppTrackingConsentCodes.CompanionDataConsent, OnDataTrackingConsentDialogClosed);
            }
#endif
        }

        public static bool GetAppTrackingConsentStatus()
        {
#if UNITY_IOS_PLAYER && UNITY_AR_COMPANION_APP_TRACKING
            return GetAppTrackingTransparencyStatus() == 1;
#else
            return PlayerPrefs.HasKey(k_DataConsentStatusPrefsKey) && PlayerPrefs.GetInt(k_DataConsentStatusPrefsKey) == 1;
#endif
        }

        public static bool HasUserProvidedTrackingConsent()
        {
#if UNITY_IOS_PLAYER
            return true;
#else
            return s_HasProvidedTrackingConsent;
#endif
        }

        public static void RequestDataExportConsent()
        {
            if (PlayerPrefs.HasKey(k_DataExportConsentStatusPrefsKey))
            {
                s_HasProvidedExportConsent = true;
            }
            else
            {
                //Show Export Consent Dialog
                CompanionIssueUtils.HandleIssue(AppTrackingConsentCodes.CompanionDataExportConsent, OnDataExportConsentDialogClosed);
            }
        }

        public static bool GetDataExportConsentStatus()
        {
            return PlayerPrefs.HasKey(k_DataExportConsentStatusPrefsKey) && PlayerPrefs.GetInt(k_DataExportConsentStatusPrefsKey) == 1;
        }

        public static bool HasUserProvidedExportConsent() { return s_HasProvidedExportConsent; }

        static void OnDataExportConsentDialogClosed(IssueHandlingResult result)
        {
            var status = result.Accept ? 1 : 0;
            PlayerPrefs.SetInt(k_DataExportConsentStatusPrefsKey, status);
            PlayerPrefs.Save();
            s_HasProvidedExportConsent = true;
        }

#if !UNITY_IOS_PLAYER
        static void OnDataTrackingConsentDialogClosed(IssueHandlingResult result)
        {
            var status = result.Accept ? 1 : 0;
            PlayerPrefs.SetInt(k_DataConsentStatusPrefsKey, status);
            PlayerPrefs.Save();
            s_HasProvidedTrackingConsent = true;
        }
#endif
    }
}
