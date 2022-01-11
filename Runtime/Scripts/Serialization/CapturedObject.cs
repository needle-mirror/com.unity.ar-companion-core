using System;
using Unity.RuntimeSceneSerialization;
using UnityEngine;

namespace Unity.AR.Companion.Core
{
    [Serializable]
    class CapturedObject : IFormatVersion
    {
        const int k_FormatVersion = 1;

        [SerializeField]
        int m_FormatVersion = k_FormatVersion;

        // ReSharper disable NotAccessedField.Local
        // Unused field just to have something for identifying the resource--the image payload is where the data resides
        [SerializeField]
        string m_Name;
        // ReSharper restore NotAccessedField.Local

        public CapturedObject() { }

        public CapturedObject(string name)
        {
            m_Name = name;
        }

        public void CheckFormatVersion()
        {
            if (m_FormatVersion != k_FormatVersion)
                throw new FormatException($"Serialization format mismatch. Expected {k_FormatVersion} but was {m_FormatVersion}.");
        }
    }
}
