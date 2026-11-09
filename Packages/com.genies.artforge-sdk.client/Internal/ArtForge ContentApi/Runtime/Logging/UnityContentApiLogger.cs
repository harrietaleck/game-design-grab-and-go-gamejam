using UnityEngine;

namespace Genies.ArtForge.ContentAPI.Logging
{
    internal sealed class UnityContentApiLogger : IContentApiLogger
    {
        public bool IsEnabled { get; set; } = true;

        public void Info(string message)
        {
            if (!IsEnabled)
            {
                return;
            }

            Debug.Log(message);
        }

        public void Warning(string message)
        {
            if (!IsEnabled)
            {
                return;
            }

            Debug.LogWarning(message);
        }

        public void Error(string message)
        {
            Debug.LogError(message);
        }
    }
}
