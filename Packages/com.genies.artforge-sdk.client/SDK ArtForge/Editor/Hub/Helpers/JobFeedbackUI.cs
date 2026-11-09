using System;
using Genies.Sdk.ArtForge.Editor.Core.Domain.Entities;
using Genies.Sdk.ArtForge.Editor.Core.Domain.Enums;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Genies.Sdk.ArtForge.Editor.Core.UI.Helpers
{
    /// <summary>
    /// Shared compact thumbs up/down controls used across queue, history, and approval preview.
    /// </summary>
    internal static class JobFeedbackUI
    {
        private const string SatisfiedIconResourcePath = "Feedback/feedback_satisfied";
        private const string UnsatisfiedIconResourcePath = "Feedback/feedback_unsatisfied";
        private const string HelpfulMenuLabel = "👍 Satisfied with result";
        private const string NotHelpfulMenuLabel = "👎 Not satisfied with result";
        private const string DefaultTooltip = "Are you satisfied with the result?";

        private static readonly Color NeutralIconColor = new Color(0.62f, 0.62f, 0.62f);
        private static readonly Color PositiveIconColor = new Color(0.38f, 0.82f, 0.52f);
        private static readonly Color NegativeIconColor = new Color(0.9f, 0.46f, 0.46f);
        private static readonly Texture2D SatisfiedIconTexture = LoadIconTexture(SatisfiedIconResourcePath, "TestPassed");
        private static readonly Texture2D UnsatisfiedIconTexture = LoadIconTexture(UnsatisfiedIconResourcePath, "TestFailed");

        public static Button CreateInlineMenuButton(
            Job job,
            Action<JobFeedbackType> onFeedbackChanged,
            string tooltip = DefaultTooltip,
            bool useButtonBackground = false,
            float buttonWidth = 22f,
            float buttonHeight = 22f,
            int iconFontSize = 20)
        {
            Button button = null;
            Image iconImage = null;
            bool isHovered = false;
            bool isPressed = false;

            Color baseBackground = useButtonBackground
                ? new Color(1f, 1f, 1f, 0.08f)
                : new Color(0f, 0f, 0f, 0f);
            Color hoverBackground = useButtonBackground
                ? new Color(1f, 1f, 1f, 0.16f)
                : new Color(1f, 1f, 1f, 0.06f);
            Color pressedBackground = useButtonBackground
                ? new Color(1f, 1f, 1f, 0.22f)
                : new Color(1f, 1f, 1f, 0.1f);

            void ApplyButtonState()
            {
                if (button == null)
                {
                    return;
                }

                button.style.backgroundColor = isPressed
                    ? pressedBackground
                    : (isHovered ? hoverBackground : baseBackground);

                if (job == null)
                {
                    if (iconImage != null)
                    {
                        iconImage.image = SatisfiedIconTexture;
                        iconImage.tintColor = NeutralIconColor;
                    }
                    button.style.color = NeutralIconColor;
                    return;
                }

                switch (job.UserFeedback)
                {
                    case JobFeedbackType.ThumbsUp:
                        if (iconImage != null)
                        {
                            iconImage.image = SatisfiedIconTexture;
                            iconImage.tintColor = PositiveIconColor;
                        }
                        button.style.color = PositiveIconColor;
                        break;
                    case JobFeedbackType.ThumbsDown:
                        if (iconImage != null)
                        {
                            iconImage.image = UnsatisfiedIconTexture;
                            iconImage.tintColor = NegativeIconColor;
                        }
                        button.style.color = NegativeIconColor;
                        break;
                    default:
                        if (iconImage != null)
                        {
                            iconImage.image = SatisfiedIconTexture;
                            iconImage.tintColor = NeutralIconColor;
                        }
                        button.style.color = NeutralIconColor;
                        break;
                }
            }

            void SetFeedback(JobFeedbackType feedbackType)
            {
                if (job == null)
                {
                    return;
                }

                job.SetUserFeedback(feedbackType);
                ApplyButtonState();
                onFeedbackChanged?.Invoke(feedbackType);
            }

            void ShowFeedbackMenu()
            {
                if (button == null || job == null)
                {
                    return;
                }

                var menu = new GenericMenu();
                menu.AddItem(
                    new GUIContent(HelpfulMenuLabel),
                    job.UserFeedback == JobFeedbackType.ThumbsUp,
                    () => SetFeedback(JobFeedbackType.ThumbsUp));
                menu.AddItem(
                    new GUIContent(NotHelpfulMenuLabel),
                    job.UserFeedback == JobFeedbackType.ThumbsDown,
                    () => SetFeedback(JobFeedbackType.ThumbsDown));
                menu.DropDown(button.worldBound);
            }

            button = new Button(ShowFeedbackMenu)
            {
                tooltip = tooltip
            };
            button.style.width = buttonWidth;
            button.style.height = buttonHeight;
            button.style.paddingLeft = 0;
            button.style.paddingRight = 0;
            button.style.paddingTop = 0;
            button.style.paddingBottom = 0;
            button.style.minWidth = buttonWidth;
            button.style.backgroundColor = baseBackground;
            button.style.borderTopLeftRadius = 4;
            button.style.borderTopRightRadius = 4;
            button.style.borderBottomLeftRadius = 4;
            button.style.borderBottomRightRadius = 4;
            button.style.borderTopWidth = 0;
            button.style.borderBottomWidth = 0;
            button.style.borderLeftWidth = 0;
            button.style.borderRightWidth = 0;
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
            button.style.justifyContent = Justify.Center;
            button.style.alignItems = Align.Center;
            button.style.fontSize = 0;

            iconImage = new Image();
            iconImage.pickingMode = PickingMode.Ignore;
            iconImage.scaleMode = ScaleMode.ScaleToFit;
            float iconSize = Mathf.Max(12f, iconFontSize);
            iconImage.style.width = Mathf.Min(buttonWidth, iconSize);
            iconImage.style.height = Mathf.Min(buttonHeight, iconSize);
            button.Add(iconImage);

            button.RegisterCallback<PointerEnterEvent>(_ =>
            {
                isHovered = true;
                ApplyButtonState();
            });
            button.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                isHovered = false;
                isPressed = false;
                ApplyButtonState();
            });
            button.RegisterCallback<PointerDownEvent>(_ =>
            {
                isPressed = true;
                ApplyButtonState();
            }, TrickleDown.TrickleDown);
            button.RegisterCallback<PointerUpEvent>(_ =>
            {
                isPressed = false;
                ApplyButtonState();
            }, TrickleDown.TrickleDown);

            ApplyButtonState();
            return button;
        }

        private static Texture2D LoadIconTexture(string resourcesPath, string editorFallbackIconName)
        {
            var texture = Resources.Load<Texture2D>(resourcesPath);
            if (texture != null)
            {
                return texture;
            }

            return EditorGUIUtility.IconContent(editorFallbackIconName).image as Texture2D;
        }
    }
}
