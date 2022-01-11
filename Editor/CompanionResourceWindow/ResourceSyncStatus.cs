using System.Collections.Generic;
using Unity.AR.Companion.CloudStorage;

namespace Unity.AR.Companion.Core
{
    class ResourceSyncState
    {
        public float DownloadProgress;
        public bool Downloading;

        Stack<RequestHandle> m_Requests;

        public Stack<RequestHandle> GetOrCreateRequestStack()
        {
            return m_Requests ?? (m_Requests = new Stack<RequestHandle>());
        }
    }
}
