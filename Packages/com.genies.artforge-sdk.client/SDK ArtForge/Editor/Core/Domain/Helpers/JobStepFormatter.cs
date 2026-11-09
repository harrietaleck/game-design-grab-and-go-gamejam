namespace Genies.Sdk.ArtForge.Editor.Core.Domain.Helpers
{
    /// <summary>
    /// Utility for formatting backend job step names into user-friendly text
    /// </summary>
    internal static class JobStepFormatter
    {
        /// <summary>
        /// Gets a friendly stage name for display (e.g., "Generating Preview")
        /// </summary>
        public static string GetStageName(string step)
        {
            if (string.IsNullOrEmpty(step))
            {
                return "Processing";
            }

            return step switch
            {
                "prompt_moderation" => "Checking Content",
                "prompt_expansion" => "Expanding Prompt",
                "image_generation" => "Generating Preview",
                "image_moderation" => "Reviewing Preview",
                "model_generation" => "Generating 3D Model",
                "model_post_processing" => "Optimizing Model",
                "model_creation" => "Creating 3D Model",
                "texture_generation" => "Generating Textures",
                "gap_submission" => "Submitting to Inventory",
                "gap_processing" => "Processing in Inventory",
                "autorig" => "Auto-Rigging",
                "universalBuild" => "Building Universal Asset",
                _ => FormatStepName(step)
            };
        }

        /// <summary>
        /// Gets a friendly progress message with percentage (e.g., "Generating preview (45%)...")
        /// </summary>
        public static string GetProgressMessage(string step, float progress)
        {
            if (string.IsNullOrEmpty(step))
            {
                return "Processing...";
            }

            return step switch
            {
                "prompt_moderation" => "Checking prompt...",
                "prompt_expansion" => "Expanding prompt...",
                "image_generation" => $"Generating preview ({progress * 100f:F0}%)...",
                "image_moderation" => "Reviewing preview...",
                "model_generation" => $"Generating 3D model ({progress * 100f:F0}%)...",
                "model_post_processing" => "Optimizing model...",
                "model_creation" => $"Creating 3D model ({progress * 100f:F0}%)...",
                "texture_generation" => $"Generating textures ({progress * 100f:F0}%)...",
                "gap_submission" => $"Submitting to Inventory ({progress * 100f:F0}%)...",
                "gap_processing" => $"Processing in Inventory ({progress * 100f:F0}%)...",
                "autorig" => "Auto-rigging...",
                "universalBuild" => "Building universal asset...",
                _ => $"{FormatStepName(step)} ({progress * 100f:F0}%)..."
            };
        }

        /// <summary>
        /// Gets a friendly name for error messages (e.g., "content moderation", "preview generation")
        /// </summary>
        public static string GetErrorContextName(string step)
        {
            if (string.IsNullOrEmpty(step))
            {
                return "processing";
            }

            return step switch
            {
                "prompt_moderation" => "content moderation",
                "prompt_expansion" => "prompt expansion",
                "image_generation" => "preview generation",
                "image_moderation" => "preview moderation",
                "model_generation" => "model generation",
                "model_post_processing" => "model post-processing",
                "model_creation" => "model creation",
                "texture_generation" => "texture generation",
                "gap_processing" => "inventory processing",
                "autorig" => "auto-rigging",
                "universalBuild" => "universal build",
                _ => FormatStepName(step).ToLowerInvariant()
            };
        }

        /// <summary>
        /// Formats a step name by replacing underscores with spaces and capitalizing
        /// </summary>
        private static string FormatStepName(string step)
        {
            if (string.IsNullOrEmpty(step))
            {
                return "Processing";
            }

            return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(step.Replace("_", " "));
        }
    }
}
