using UnityEngine;
using UnityEngine.UIElements;

namespace Genies.Sdk.ArtForge.Editor.Core.UI.Helpers
{
    /// <summary>
    /// Utility class for consistent styling across the application.
    /// Provides theme colors and common style helpers.
    /// </summary>
    internal static class StyleHelpers
    {
        // Theme Colors
        public static class Colors
        {
            // Background colors
            public static readonly Color BackgroundDark = new Color(0.117f, 0.117f, 0.117f);      // #1E1E1E
            public static readonly Color BackgroundMedium = new Color(0.196f, 0.196f, 0.196f);    // #323232
            public static readonly Color BackgroundLight = new Color(0.25f, 0.25f, 0.25f);        // #404040

            // Text colors
            public static readonly Color TextPrimary = new Color(0.86f, 0.86f, 0.86f);            // #DBDBDB
            public static readonly Color TextSecondary = new Color(0.8f, 0.8f, 0.8f);             // #CCCCCC
            public static readonly Color TextHint = new Color(0.6f, 0.6f, 0.6f);                  // #999999

            // Border colors
            public static readonly Color BorderDark = new Color(0.117f, 0.117f, 0.117f);
            public static readonly Color BorderMedium = new Color(0.4f, 0.4f, 0.4f);

            // Feature category colors
            public static readonly Color Generate = new Color(0.2f, 0.7f, 1.0f);      // Blue
            public static readonly Color Edit = new Color(1.0f, 0.6f, 0.2f);          // Orange
            public static readonly Color Project = new Color(0.6f, 0.4f, 1.0f);       // Purple
            public static readonly Color Services = new Color(0.2f, 0.9f, 0.6f);      // Green
            public static readonly Color Settings = new Color(0.9f, 0.9f, 0.2f);      // Yellow

            // Status colors
            public static readonly Color Success = new Color(0.0f, 0.8f, 0.4f);
            public static readonly Color Warning = new Color(1.0f, 0.6f, 0.0f);
            public static readonly Color Error = new Color(1.0f, 0.2f, 0.2f);

            // Accent color (for highlights, drag-drop, etc.)
            public static readonly Color Accent = new Color(0.12f, 0.6f, 0.87f);  // Blue accent
        }

        /// <summary>
        /// Apply primary button style with category-specific color
        /// </summary>
        public static void ApplyPrimaryButtonStyle(Button button, string category = "Project", int width = -1)
        {
            var categoryColor = GetCategoryColor(category);

            button.style.backgroundColor = categoryColor;
            button.style.color = Color.white;
            button.style.paddingTop = 10;
            button.style.paddingBottom = 10;
            button.style.paddingLeft = 20;
            button.style.paddingRight = 20;
            button.style.borderTopLeftRadius = 3;
            button.style.borderTopRightRadius = 3;
            button.style.borderBottomLeftRadius = 3;
            button.style.borderBottomRightRadius = 3;
            button.style.borderTopWidth = 0;
            button.style.borderBottomWidth = 0;
            button.style.borderLeftWidth = 0;
            button.style.borderRightWidth = 0;
            button.style.fontSize = 13;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;

            if (width > 0)
            {
                button.style.width = width;
            }

            // Hover effect
            button.RegisterCallback<MouseEnterEvent>(evt =>
            {
                if (!button.enabledInHierarchy)
                {
                    return;
                }

                var darkerColor = new Color(
                    categoryColor.r * 0.8f,
                    categoryColor.g * 0.8f,
                    categoryColor.b * 0.8f
                );
                button.style.backgroundColor = darkerColor;
            });

            button.RegisterCallback<MouseLeaveEvent>(evt =>
            {
                if (!button.enabledInHierarchy)
                {
                    return;
                }

                button.style.backgroundColor = categoryColor;
            });
        }

        /// <summary>
        /// Apply secondary button style
        /// </summary>
        public static void ApplySecondaryButtonStyle(Button button, int width = -1)
        {
            button.style.backgroundColor = new Color(0.25f, 0.25f, 0.25f);
            button.style.color = Colors.TextSecondary;
            button.style.paddingTop = 8;
            button.style.paddingBottom = 8;
            button.style.paddingLeft = 16;
            button.style.paddingRight = 16;
            button.style.borderTopLeftRadius = 3;
            button.style.borderTopRightRadius = 3;
            button.style.borderBottomLeftRadius = 3;
            button.style.borderBottomRightRadius = 3;
            button.style.borderTopWidth = 1;
            button.style.borderBottomWidth = 1;
            button.style.borderLeftWidth = 1;
            button.style.borderRightWidth = 1;
            button.style.borderTopColor = Colors.BorderMedium;
            button.style.borderBottomColor = Colors.BorderMedium;
            button.style.borderLeftColor = Colors.BorderMedium;
            button.style.borderRightColor = Colors.BorderMedium;
            button.style.fontSize = 12;

            if (width > 0)
            {
                button.style.width = width;
            }

            // Hover effect
            button.RegisterCallback<MouseEnterEvent>(evt =>
            {
                if (!button.enabledInHierarchy)
                {
                    return;
                }

                button.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f);
            });

            button.RegisterCallback<MouseLeaveEvent>(evt =>
            {
                if (!button.enabledInHierarchy)
                {
                    return;
                }

                button.style.backgroundColor = new Color(0.25f, 0.25f, 0.25f);
            });
        }

        /// <summary>
        /// Create a styled header element
        /// </summary>
        public static VisualElement CreateHeader(string title)
        {
            var header = new VisualElement();
            header.style.backgroundColor = Colors.BackgroundMedium;
            header.style.paddingLeft = 15;
            header.style.paddingRight = 15;
            header.style.paddingTop = 15;
            header.style.paddingBottom = 15;
            header.style.marginBottom = 20;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = Colors.BorderDark;

            var label = new Label(title);
            label.style.fontSize = 18;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.color = Colors.TextPrimary;
            header.Add(label);

            return header;
        }

        /// <summary>
        /// Create a styled section container
        /// </summary>
        public static VisualElement CreateSection(string sectionTitle)
        {
            var section = new VisualElement();
            section.style.marginBottom = 20;

            if (!string.IsNullOrEmpty(sectionTitle))
            {
                var title = new Label(sectionTitle);
                title.style.fontSize = 14;
                title.style.unityFontStyleAndWeight = FontStyle.Bold;
                title.style.marginBottom = 10;
                title.style.color = Colors.TextSecondary;
                section.Add(title);
            }

            return section;
        }

        /// <summary>
        /// Get category-specific color
        /// </summary>
        public static Color GetCategoryColor(string category)
        {
            return category switch
            {
                "Generate" => Colors.Generate,
                "Edit" => Colors.Edit,
                "Project" => Colors.Project,
                "Services" => Colors.Services,
                "Settings" => Colors.Settings,
                _ => Colors.Project // Default
            };
        }
    }
}
