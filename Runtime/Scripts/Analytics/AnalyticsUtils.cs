using System.Collections;
using Unity.AR.Companion.Core;
using UnityEngine;

#if INCLUDE_DELTA_DNA
using DeltaDNA;
#endif

namespace Unity.AR.Companion.Analytics
{
    enum UserRole
    {
        Anonymous,
        SignedIn
    }

    static class AnalyticsUtils
    {
        public const string UserRoleParamName = "userRole";
        const string k_ProjectIdParamName = "projectId";
        public static string CurrentProjectId { get; set; }
        public static UserRole CurrentUserRole { get; set; }

#if INCLUDE_DELTA_DNA
        public static bool HasAnalyticsSDKStarted => DDNA.Instance.HasStarted;
#endif

        public static void Initialize()
        {
            // Start DDNA synchronously to avoid error from OnApplicationPause when entering play mode
#if INCLUDE_DELTA_DNA && UNITY_EDITOR
            DDNA.Instance.SetPiplConsent(UserDataConsentUtils.GetAppTrackingConsentStatus(), UserDataConsentUtils.GetDataExportConsentStatus());
            DDNA.Instance.StartSDK();
#endif
            CoroutineUtils.StartCoroutine(RequestConsentAndInitializeAnalytics());
        }

#if INCLUDE_DELTA_DNA
        public static GameEvent GetGameEventWithProjectID(string eventName)
        {
            var gameEvent = new GameEvent(eventName);
            if (!string.IsNullOrEmpty(CurrentProjectId))
                gameEvent.AddParam(k_ProjectIdParamName, CurrentProjectId);

            return gameEvent;
        }
#endif

        static IEnumerator RequestConsentAndInitializeAnalytics()
        {
            UserDataConsentUtils.RequestAppTrackingConsent();

            // WaitUntil did not suspend the coroutine as expected, so using while loop.
            while (!UserDataConsentUtils.HasUserProvidedTrackingConsent())
            {
                yield return null;
            }

#if INCLUDE_DELTA_DNA && !UNITY_EDITOR
            DDNA.Instance.IsPiplConsentRequired(delegate(bool isRequired)
            {
                CoroutineUtils.StartCoroutine(WaitUntilUserProvidesExportConsent(isRequired));
            });
#endif
        }

#if INCLUDE_DELTA_DNA && !UNITY_EDITOR
        static IEnumerator WaitUntilUserProvidesExportConsent(bool isRequired)
        {
            var hasDataUseConsent =  UserDataConsentUtils.GetAppTrackingConsentStatus();
            var hasDataExportConsent = true;

            if (isRequired)
            {
                // Implement a consent flow here and update the booleans hasDataUseConsent and hasDataExportConsent accordingly
                UserDataConsentUtils.RequestDataExportConsent();
                while (!UserDataConsentUtils.HasUserProvidedExportConsent())
                {
                    yield return null;
                }

                hasDataExportConsent = UserDataConsentUtils.GetDataExportConsentStatus();
            }

            DDNA.Instance.SetPiplConsent(hasDataUseConsent, hasDataExportConsent);

            // Start collecting data
            DDNA.Instance.StartSDK();
        }
#endif
    }
}
