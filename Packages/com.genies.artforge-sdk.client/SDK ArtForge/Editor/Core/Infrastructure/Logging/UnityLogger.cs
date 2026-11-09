using System;
using ILogger = Genies.Sdk.ArtForge.Editor.Core.Application.Interfaces.ILogger;

namespace Genies.Sdk.ArtForge.Editor.Core.Infrastructure.Logging
{
    /// <summary>
    /// Unity-specific implementation of ILogger.
    /// Wraps Unity's Debug.Log functionality.
    /// </summary>
    internal class UnityLogger : ILogger
    {
        private const string Prefix = "[GeniesArtForge]";
        private const int LogsEnabledDefault = 1;
        public const string LogsEnabledPrefKey = "GAF_Settings_LogsEnabled";

        private static volatile bool _logsEnabled = true;

        public static bool LogsEnabled
        {
            get => _logsEnabled;
            set => _logsEnabled = value;
        }

        public static void RefreshLogsEnabledFromPrefs()
        {
#if UNITY_EDITOR
            _logsEnabled = UnityEditor.EditorPrefs.GetInt(LogsEnabledPrefKey, LogsEnabledDefault) == LogsEnabledDefault;
#endif
        }

        public void Info(string message)
        {
            if (!LogsEnabled)
            {
                return;
            }

            UnityEngine.Debug.Log($"{Prefix} {message}");
        }

        public void Warning(string message)
        {
            if (!LogsEnabled)
            {
                return;
            }

            UnityEngine.Debug.LogWarning($"{Prefix} {message}");
        }

        public void Error(string message)
        {
            UnityEngine.Debug.LogError($"{Prefix} {message}");
        }

        public void Error(string message, Exception exception)
        {
            UnityEngine.Debug.LogError($"{Prefix} {message}");
            if (exception != null)
            {
                UnityEngine.Debug.LogException(exception);
            }
        }

        public void Debug(string message)
        {
#if UNITY_EDITOR
            if (!LogsEnabled)
            {
                return;
            }

            UnityEngine.Debug.Log($"{Prefix} [DEBUG] {message}");
#endif
        }
    }
}
