using UnityEngine;

#if INCLUDE_DELTA_DNA
using System;
using DeltaDNA;
#endif

namespace Unity.AR.Companion.Analytics
{
    enum UIAction
    {
        ShowView
    }

    enum UILocation
    {
        WelcomeScreen,
        ProjectListMenu,
        Profile,
        ImportProject,
        ResourceListMenu,
        Home,
        Proxy,
        Environment,
        RecordData,
        Marker,
        ObjectCapture
    }

    static class UIInteractionEvent
    {
        const string k_EventName = "uiInteraction";
        const string k_ActionParamName = "UIAction";
        const string k_LocationParamName = "UILocation";

        public static void SendEvent(UIAction action, UILocation location)
        {
#if INCLUDE_DELTA_DNA
            if (!Application.isPlaying)
                return;

            try
            {
                DDNA.Instance.RecordEvent(AnalyticsUtils.GetGameEventWithProjectID(k_EventName)
                    .AddParam(AnalyticsUtils.UserRoleParamName, AnalyticsUtils.CurrentUserRole)
                    .AddParam(k_ActionParamName, action.ToString())
                    .AddParam(k_LocationParamName, location.ToString()));
            }
            catch (Exception exception)
            {
                Debug.LogError("Caught an exception trying to send analytics event");
                Debug.LogException(exception);
            }
#endif
        }
    }
}
