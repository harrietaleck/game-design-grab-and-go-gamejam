using System.Collections.Generic;
using UnityEditor;

namespace Genies.Sdk.ArtForge.Editor.Core.Infrastructure.Helpers
{
    internal static class EditorSfx
    {
        private const double DedupIntervalSeconds = 0.15d;
        private const string PreviewReadyStage = "preview_ready";
        private const string Model3DCompletedStage = "model3d_completed";
        private const string GapSubmissionCompletedStage = "gap_submission_completed";

        private static readonly Dictionary<string, double> LastPlayedByKey = new Dictionary<string, double>();

        public static void PlayPreviewReady(string jobId = null)
        {
            Play(PreviewReadyStage, jobId);
        }

        public static void PlayModel3DCompleted(string jobId = null)
        {
            Play(Model3DCompletedStage, jobId);
        }

        public static void PlayGapSubmissionCompleted(string jobId = null)
        {
            Play(GapSubmissionCompletedStage, jobId);
        }

        private static void Play(string stage, string jobId)
        {
            string key = string.IsNullOrEmpty(jobId)
                ? stage
                : $"{stage}:{jobId}";

            double now = EditorApplication.timeSinceStartup;
            if (LastPlayedByKey.TryGetValue(key, out double lastPlayedAt) &&
                now - lastPlayedAt < DedupIntervalSeconds)
            {
                return;
            }

            LastPlayedByKey[key] = now;
            EditorApplication.Beep();
        }
    }
}
