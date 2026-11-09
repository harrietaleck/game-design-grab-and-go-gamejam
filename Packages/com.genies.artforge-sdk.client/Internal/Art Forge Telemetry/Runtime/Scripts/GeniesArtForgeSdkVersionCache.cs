using System;
using UnityEngine;

namespace Genies.ArtForge.Telemetry
{
    
#if GENIES_SDK && !GENIES_INTERNAL
    [Serializable]
    internal class GeniesArtForgeSdkVersionCache : ScriptableObject
#else
    [Serializable]
    public class GeniesArtForgeSdkVersionCache : ScriptableObject
        
#endif
    {
        public string Version;
        public string PackageName;
        public string LastUpdatedUtc;
        public string Notes;
    }
}
