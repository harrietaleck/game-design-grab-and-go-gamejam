using System.Collections.Generic;
using System.Linq;
using Genies.Sdk.ArtForge.Editor.Core.UI;
using Genies.Sdk.ArtForge.Editor.Core.Infrastructure;

namespace Genies.Sdk.ArtForge.Editor.Hub
{
    /// <summary>
    /// Navigation item in the sidebar
    /// </summary>
    internal class NavigationItem
    {
        public string Id { get; set; }
        public string Label { get; set; }
        public string ViewName { get; set; }  // null for parent items
        public string FeatureId { get; set; }  // Unique identifier for feature availability
        public List<NavigationItem> Children { get; set; } = new List<NavigationItem>();

        public bool IsParent => Children.Count > 0;
    }

    /// <summary>
    /// Feature availability data
    /// </summary>
    internal class FeatureAvailability
    {
        public string FeatureId { get; set; }
        public bool IsAvailable { get; set; }
    }

    /// <summary>
    /// Configuration for hub navigation and view registration.
    /// Single source of truth for all navigation structure.
    /// Handles feature visibility based on availability.
    /// </summary>
    internal class NavigationConfig
    {
        private List<NavigationItem> _allNavigationItems;
        private Dictionary<string, FeatureAvailability> _featureAvailability;

        public NavigationConfig()
        {
            _allNavigationItems = BuildAllNavigationItems();
            InitializeDefaultAvailability();
            UpdateFeatureAvailabilityFromService(); // Auto-update based on FeatureAvailabilityService
        }

        /// <summary>
        /// Initialize all features as available by default
        /// </summary>
        private void InitializeDefaultAvailability()
        {
            _featureAvailability = new Dictionary<string, FeatureAvailability>
            {
                { "project-setup", new FeatureAvailability { FeatureId = "project-setup", IsAvailable = true } },
                { "model-generator", new FeatureAvailability { FeatureId = "model-generator", IsAvailable = true } },
                { "model-editor", new FeatureAvailability { FeatureId = "model-editor", IsAvailable = true } },
                { "history", new FeatureAvailability { FeatureId = "history", IsAvailable = true } },
                { "settings", new FeatureAvailability { FeatureId = "settings", IsAvailable = true } }
            };
        }

        /// <summary>
        /// Update feature availability from centralized FeatureAvailabilityService
        /// </summary>
        private void UpdateFeatureAvailabilityFromService()
        {
            var features = FeatureAvailabilityService.GetNavigationFeatureAvailability();
            UpdateFeatureAvailability(features);
        }

        /// <summary>
        /// Update feature availability from backend data
        /// Call this when you receive availability data from the server
        /// </summary>
        public void UpdateFeatureAvailability(List<FeatureAvailability> availabilityData)
        {
            if (availabilityData == null)
            {
                return;
            }

            foreach (var feature in availabilityData)
            {
                if (_featureAvailability.ContainsKey(feature.FeatureId))
                {
                    _featureAvailability[feature.FeatureId] = feature;
                }
            }
        }

        /// <summary>
        /// Check if a specific feature is available
        /// </summary>
        public bool IsFeatureAvailable(string featureId)
        {
            return _featureAvailability.TryGetValue(featureId, out var availability)
                   && availability.IsAvailable;
        }

        /// <summary>
        /// Get feature availability details
        /// </summary>
        public FeatureAvailability GetFeatureAvailability(string featureId)
        {
            return _featureAvailability.TryGetValue(featureId, out var availability)
                ? availability
                : new FeatureAvailability { FeatureId = featureId, IsAvailable = false };
        }

        /// <summary>
        /// Get the navigation tree structure (filtered by availability)
        /// </summary>
        public List<NavigationItem> GetNavigationTree()
        {
            return FilterByAvailability(_allNavigationItems);
        }

        /// <summary>
        /// Build all navigation items (called once in constructor)
        /// Can be serialized to JSON for persistence
        /// </summary>
        private List<NavigationItem> BuildAllNavigationItems()
        {
            return new List<NavigationItem>
            {
                // Project Setup (top-level)
                new NavigationItem
                {
                    Id = "nav-project-setup",
                    Label = "Project Setup",
                    ViewName = "ProjectSetup",
                    FeatureId = "project-setup"
                },

                // Model Generator (top-level)
                new NavigationItem
                {
                    Id = "nav-model-generator",
                    Label = "Model Generator",
                    ViewName = "ModelGenerator",
                    FeatureId = "model-generator"
                },

                // History (top-level) - NEW
                new NavigationItem
                {
                    Id = "nav-history",
                    Label = "History",
                    ViewName = "History",
                    FeatureId = "history"
                },

                // Model Editor (top-level)
                new NavigationItem
                {
                    Id = "nav-model-editor",
                    Label = "Model Editor",
                    ViewName = "ModelEditor",
                    FeatureId = "model-editor"
                },

                // Settings (top-level)
                new NavigationItem
                {
                    Id = "nav-settings",
                    Label = "Settings",
                    ViewName = "Settings",
                    FeatureId = "settings"
                }
            };
        }

        /// <summary>
        /// Filter navigation tree based on feature availability
        /// Removes parent items if all children are unavailable
        /// </summary>
        private List<NavigationItem> FilterByAvailability(List<NavigationItem> items)
        {
            var filteredItems = new List<NavigationItem>();

            foreach (var item in items)
            {
                if (item.IsParent)
                {
                    // Filter children
                    var availableChildren = item.Children
                        .Where(child => string.IsNullOrEmpty(child.FeatureId) || IsFeatureAvailable(child.FeatureId))
                        .ToList();

                    // Only include parent if it has available children
                    if (availableChildren.Any())
                    {
                        var filteredParent = new NavigationItem
                        {
                            Id = item.Id,
                            Label = item.Label,
                            ViewName = item.ViewName,
                            FeatureId = item.FeatureId,
                            Children = availableChildren
                        };
                        filteredItems.Add(filteredParent);
                    }
                }
                else
                {
                    // Leaf item - check availability
                    if (string.IsNullOrEmpty(item.FeatureId) || IsFeatureAvailable(item.FeatureId))
                    {
                        filteredItems.Add(item);
                    }
                }
            }

            return filteredItems;
        }

        /// <summary>
        /// View controller factory - maps view names to view controller instances
        /// </summary>
        public IViewController CreateViewController(string viewName)
        {
            return viewName switch
            {
                "ProjectSetup" => new Components.ProjectSetup.ProjectSetupView(),
                "ModelGenerator" => new Components.ModelGenerator.ModelGeneratorView(),
                "History" => new Components.History.HistoryView(),
                "ModelEditor" => new Components.ModelEditor.ModelEditorView(),
                "Settings" => new Components.Settings.SettingsView(),
                _ => null
            };
        }

        /// <summary>
        /// Get the default view to load on startup
        /// Delegates to FeatureAvailabilityService
        /// </summary>
        public string GetDefaultView()
        {
            return FeatureAvailabilityService.GetDefaultView();
        }

        /// <summary>
        /// Get the default expanded section on startup
        /// Delegates to FeatureAvailabilityService
        /// </summary>
        public string GetDefaultExpandedSection()
        {
            return FeatureAvailabilityService.GetDefaultExpandedSection();
        }
    }
}

