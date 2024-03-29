﻿using System;
using Unity.XRTools.ModuleLoader;

namespace Unity.AR.Companion.Core
{
    interface IProvidesIssueHandling : IFunctionalityProvider
    {
        void RaiseIssueRequest(in IssueHandlingRequest request);

        void RaiseIssueRequest(string issueCode, IssueDialogSettings? settings = null,
            IssueHandledCallback callback = null, bool useToggle = false, bool toggleStatus = false);

        void RaiseIssueRequest(string description, string issueCode, Action closedCallback = null);

        void RaiseIssueRequest(string issueCode, Action closedCallback);

        void RaiseIssueRequest(string issueCode, Exception exception);

        bool GetIssueDialogSettings(string issueCode, out IssueDialogSettings settings);

        bool IsIgnored(string issueCode);

        event IssueHandlingRequested OnIssueRaised;
    }
}
