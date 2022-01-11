using System;
using Unity.XRTools.ModuleLoader;

namespace Unity.AR.Companion.Core
{
    static class CompanionIssueUtils
    {
        static bool CanHandleIssues(out IssueHandlingModule issueHandling)
        {
            issueHandling = ModuleLoaderCore.instance.GetModule<IssueHandlingModule>();
            if(issueHandling == null)
                return false;

            return true;
        }

        internal static void HandleIssue(string issueCode, Exception exception)
        {
            if(!CanHandleIssues(out var issueHandling))
                return;

            issueHandling.GetIssueDialogSettings(issueCode, out var settings);
            issueHandling.RaiseIssueRequest(new IssueHandlingRequest(issueCode, settings, exception));
        }

        internal static void HandleIssue(string issueCode)
        {
            if(!CanHandleIssues(out var issueHandling))
                return;

            issueHandling.RaiseIssueRequest(issueCode);
        }

        internal static void HandleIssue(string issueCode, string additionalInfo)
        {
            if(!CanHandleIssues(out var issueHandling))
                return;

            issueHandling.GetIssueDialogSettings(issueCode, out var settings);
            settings.Description = $"{settings.Description}\n{additionalInfo}";
            issueHandling.RaiseIssueRequest(issueCode);
        }
    }
}
