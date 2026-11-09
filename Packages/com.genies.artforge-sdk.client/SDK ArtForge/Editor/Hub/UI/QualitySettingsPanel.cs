using System;
using System.Collections.Generic;
using Genies.Sdk.ArtForge.Editor.Core.Domain.Helpers;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace Genies.Sdk.ArtForge.Editor.Hub.UI
{
    /// <summary>
    /// Reusable quality settings panel with collapsible foldout and compact horizontal layout.
    /// Shows: Pivot, Topology, Polycount, LODs, Collider, Texture Resolution, Aspect Ratio, AO, Transparent.
    /// Mesh settings can be hidden for specific modes (e.g. Decor 2D).
    /// </summary>
    internal class QualitySettingsPanel
    {
        public VisualElement Root { get; private set; }
        public Foldout Foldout => _foldout;

        public event Action OnQueueGenerationRequested;
        public event Action OnNavigateBackRequested;
        public event Action OnNavigateForwardRequested;

        private Foldout _foldout;
        private VisualElement _tableContainer;

        private VisualElement _pivotColumn;
        private ToolbarMenu _pivotDropdown;
        private VisualElement _topologyColumn;
        private ToolbarMenu _topologyDropdown;
        private VisualElement _polycountColumn;
        private TextField _polycountField;
        private VisualElement _lodsColumn;
        private Toggle _lodsToggle;
        private VisualElement _colliderColumn;
        private Toggle _colliderToggle;
        private ToolbarMenu _textureResDropdown;
        private VisualElement _aspectRatioColumn;
        private ToolbarMenu _aspectRatioDropdown;
        private VisualElement _aoColumn;
        private Toggle _aoToggle;
        private VisualElement _transparentColumn;
        private Toggle _transparentToggle;

        private Func<string> _getPivot;
        private Action<string> _setPivot;
        private Func<string> _getTopology;
        private Action<string> _setTopology;
        private Func<int> _getPolycount;
        private Action<int> _setPolycount;
        private Func<bool> _getLODs;
        private Action<bool> _setLODs;
        private Func<bool> _getCollider;
        private Action<bool> _setCollider;
        private Func<string> _getTexRes;
        private Action<string> _setTexRes;
        private Func<string> _getAspectRatio;
        private Action<string> _setAspectRatio;
        private Func<IReadOnlyList<string>> _getAspectRatioChoices;
        private Func<bool> _getAO;
        private Action<bool> _setAO;
        private Func<bool> _getTransparent;
        private Action<bool> _setTransparent;
        private Func<bool> _allowTopologySelection;
        private Func<bool> _allowAspectRatioSelection;
        private Func<bool> _showMeshSettings;
        private Func<bool> _showTransparentSetting;
        private bool _topologySelectionEnabled = true;

        private const string PrefPivot = "Pivot";
        private const string PrefTopology = "Topology";
        private const string PrefPolycount = "Polycount";
        private const string PrefLODs = "Lods";
        private const string PrefCollider = "Collider";
        private const string PrefTextureRes = "TextureRes";
        private const string PrefAspectRatio = "AspectRatio";
        private const string PrefAO = "AO";
        private const string PrefTransparent = "Transparent";
        private const string HardcodedTopology = "Quads";
        private const string DefaultAspectRatio = "1:1";
        private static readonly string[] DefaultAspectRatios =
        {
            "1:1", "1:4", "4:1", "1:8", "8:1", "2:3", "3:2",
            "3:4", "4:3", "4:5", "5:4", "9:16", "16:9", "21:9"
        };

        private string _prefsKeyPrefix;

        public QualitySettingsPanel()
        {
            BuildUI();
        }

        public bool GetAOEnabled()
        {
            return _aoToggle?.value ?? false;
        }

        public void FocusPolycountField()
        {
            _polycountField?.Focus();
        }

        private void BuildUI()
        {
            Root = new VisualElement();
            Root.style.marginBottom = 10;
            Root.style.backgroundColor = new Color(45f / 255f, 45f / 255f, 45f / 255f);
            Root.style.borderBottomColor = new Color(60f / 255f, 60f / 255f, 60f / 255f);
            Root.style.borderTopColor = new Color(60f / 255f, 60f / 255f, 60f / 255f);
            Root.style.borderLeftColor = new Color(60f / 255f, 60f / 255f, 60f / 255f);
            Root.style.borderRightColor = new Color(60f / 255f, 60f / 255f, 60f / 255f);
            Root.style.borderBottomWidth = 1;
            Root.style.borderTopWidth = 1;
            Root.style.borderLeftWidth = 1;
            Root.style.borderRightWidth = 1;
            Root.style.borderBottomLeftRadius = 4;
            Root.style.borderBottomRightRadius = 4;
            Root.style.borderTopLeftRadius = 4;
            Root.style.borderTopRightRadius = 4;
            Root.style.paddingBottom = 8;
            Root.style.paddingTop = 8;
            Root.style.paddingLeft = 8;
            Root.style.paddingRight = 8;

            _foldout = new Foldout();
            _foldout.text = "Quality Settings";
            _foldout.value = true;
            _foldout.style.unityFontStyleAndWeight = FontStyle.Bold;

            _tableContainer = new VisualElement();
            _tableContainer.style.flexDirection = FlexDirection.Row;
            _tableContainer.style.marginTop = 5;
            _tableContainer.style.paddingLeft = 10;
            _tableContainer.style.paddingRight = 10;
            _tableContainer.style.paddingTop = 5;
            _tableContainer.style.paddingBottom = 5;

            CreatePivotColumn();
            CreateTopologyColumn();
            CreatePolycountColumn();
            CreateLODsColumn();
            CreateColliderColumn();
            CreateTextureResColumn();
            CreateAspectRatioColumn();
            CreateAOColumn();
            CreateTransparentColumn();

            _foldout.Add(_tableContainer);
            Root.Add(_foldout);
        }

        private void CreatePivotColumn()
        {
            _pivotColumn = CreateColumn("Pivot", 80);
            _pivotDropdown = new ToolbarMenu();
            _pivotDropdown.text = "Base";
            _pivotDropdown.style.width = 80;
            _pivotDropdown.style.fontSize = 11;
            _pivotColumn.Add(_pivotDropdown);
            _tableContainer.Add(_pivotColumn);
        }

        private void CreateTopologyColumn()
        {
            _topologyColumn = CreateColumn("Topology", 90);
            _topologyDropdown = new ToolbarMenu();
            _topologyDropdown.text = HardcodedTopology;
            _topologyDropdown.style.width = 90;
            _topologyDropdown.style.fontSize = 11;
            _topologyColumn.Add(_topologyDropdown);
            _tableContainer.Add(_topologyColumn);
        }

        private void CreatePolycountColumn()
        {
            _polycountColumn = CreateColumn("Polycount", 80);
            _polycountField = new TextField();
            _polycountField.value = QualityDefaults.Polycount.ToString();
            _polycountField.style.width = 80;
            _polycountField.style.fontSize = 11;
            _polycountColumn.Add(_polycountField);
            _tableContainer.Add(_polycountColumn);
        }

        private void CreateLODsColumn()
        {
            _lodsColumn = CreateColumn("LODs", 50);
            _lodsToggle = new Toggle();
            _lodsToggle.value = false;
            _lodsColumn.Add(_lodsToggle);
            _tableContainer.Add(_lodsColumn);
        }

        private void CreateColliderColumn()
        {
            _colliderColumn = CreateColumn("Collider", 60);
            _colliderToggle = new Toggle();
            _colliderToggle.value = false;
            _colliderColumn.Add(_colliderToggle);
            _tableContainer.Add(_colliderColumn);
        }

        private void CreateTextureResColumn()
        {
            var column = CreateColumn("Tex Res", 70);
            _textureResDropdown = new ToolbarMenu();
            _textureResDropdown.text = "1024";
            _textureResDropdown.style.width = 70;
            _textureResDropdown.style.fontSize = 11;
            column.Add(_textureResDropdown);
            _tableContainer.Add(column);
        }

        private void CreateAOColumn()
        {
            _aoColumn = CreateColumn("AO", 40);
            _aoToggle = new Toggle();
            _aoToggle.value = false;
            _aoToggle.RegisterValueChangedCallback(evt =>
            {
                UpdateSummary();
            });
            _aoColumn.Add(_aoToggle);
            _tableContainer.Add(_aoColumn);
        }

        private void CreateAspectRatioColumn()
        {
            _aspectRatioColumn = CreateColumn("Aspect", 70);
            _aspectRatioDropdown = new ToolbarMenu();
            _aspectRatioDropdown.text = DefaultAspectRatio;
            _aspectRatioDropdown.style.width = 70;
            _aspectRatioDropdown.style.fontSize = 11;
            _aspectRatioColumn.Add(_aspectRatioDropdown);
            _tableContainer.Add(_aspectRatioColumn);
        }

        private void CreateTransparentColumn()
        {
            _transparentColumn = CreateColumn("Transparent", 85);
            _transparentToggle = new Toggle();
            _transparentToggle.value = false;
            _transparentColumn.Add(_transparentToggle);
            _tableContainer.Add(_transparentColumn);
        }

        private VisualElement CreateColumn(string label, int width)
        {
            var column = new VisualElement();
            column.style.width = width;
            column.style.alignItems = Align.FlexStart;
            column.style.marginRight = 8;

            var labelElement = new Label(label);
            labelElement.style.fontSize = 10;
            labelElement.style.color = new Color(0.7f, 0.7f, 0.7f);
            labelElement.style.marginBottom = 3;
            labelElement.style.unityTextAlign = TextAnchor.MiddleLeft;

            column.Add(labelElement);
            return column;
        }

        public void Initialize(
            Func<string> getPivot, Action<string> setPivot,
            Func<string> getTopology, Action<string> setTopology,
            Func<int> getPolycount, Action<int> setPolycount,
            Func<bool> getLODs, Action<bool> setLODs,
            Func<bool> getCollider, Action<bool> setCollider,
            Func<string> getTexRes, Action<string> setTexRes,
            Func<bool> getAO = null, Action<bool> setAO = null,
            Func<string> getAspectRatio = null, Action<string> setAspectRatio = null,
            Func<IReadOnlyList<string>> getAspectRatioChoices = null,
            Func<bool> allowTopologySelection = null,
            Func<bool> allowAspectRatioSelection = null,
            Func<bool> showMeshSettings = null,
            Func<bool> getTransparent = null, Action<bool> setTransparent = null,
            Func<bool> showTransparentSetting = null,
            string prefsKeyPrefix = null)
        {
            _getPivot = getPivot;
            _setPivot = setPivot;
            _getTopology = getTopology;
            _setTopology = setTopology;
            _getPolycount = getPolycount;
            _setPolycount = setPolycount;
            _getLODs = getLODs;
            _setLODs = setLODs;
            _getCollider = getCollider;
            _setCollider = setCollider;
            _getTexRes = getTexRes;
            _setTexRes = setTexRes;
            _getAO = getAO;
            _setAO = setAO;
            _getTransparent = getTransparent;
            _setTransparent = setTransparent;
            _getAspectRatio = getAspectRatio;
            _setAspectRatio = setAspectRatio;
            _getAspectRatioChoices = getAspectRatioChoices;
            _allowTopologySelection = allowTopologySelection;
            _allowAspectRatioSelection = allowAspectRatioSelection;
            _showMeshSettings = showMeshSettings;
            _showTransparentSetting = showTransparentSetting;

            _prefsKeyPrefix = string.IsNullOrWhiteSpace(prefsKeyPrefix) ? null : prefsKeyPrefix.Trim();

            SetupPivotDropdown();
            SetupTopologyDropdown();
            SetupPolycountField();
            SetupLODsToggle();
            SetupColliderToggle();
            SetupTextureResDropdown();
            SetupAOToggle();
            SetupTransparentToggle();
            SetupAspectRatioDropdown();

            ApplyEditorPrefsToSettings();
            LoadValues();
            UpdateMeshSettingsVisibility();
            UpdateTopologySelection();
            UpdateAspectRatioSelection();
            UpdateTransparentSettingVisibility();
            UpdateSummary();
        }

        public void RefreshValues()
        {
            LoadValues();
            UpdateMeshSettingsVisibility();
            UpdateTopologySelection();
            UpdateAspectRatioSelection();
            UpdateTransparentSettingVisibility();
            UpdateSummary();
        }

        private void SetupPivotDropdown()
        {
            _pivotDropdown.menu.MenuItems().Clear();
            _pivotDropdown.menu.AppendAction("Base", _ =>
            {
                _pivotDropdown.text = "Base";
                _setPivot("Base");
                SavePrefString(PrefPivot, "Base");
                UpdateSummary();
            });
            _pivotDropdown.menu.AppendAction("Center", _ =>
            {
                _pivotDropdown.text = "Center";
                _setPivot("Center");
                SavePrefString(PrefPivot, "Center");
                UpdateSummary();
            });
        }

        private void SetupTopologyDropdown()
        {
            _topologyDropdown.menu.MenuItems().Clear();
            _topologyDropdown.menu.AppendAction("Triangles", _ =>
            {
                _topologyDropdown.text = "Triangles";
                _setTopology("Triangles");
                SavePrefString(PrefTopology, "Triangles");
                ValidatePolycountLimits();
                UpdateSummary();
            });
            _topologyDropdown.menu.AppendAction("Quads", _ =>
            {
                _topologyDropdown.text = "Quads";
                _setTopology("Quads");
                SavePrefString(PrefTopology, "Quads");
                ValidatePolycountLimits();
                UpdateSummary();
            });
        }

        private void SetupPolycountField()
        {
            _polycountField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if ((evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter) && evt.ctrlKey)
                {
                    evt.StopImmediatePropagation();
                    evt.StopPropagation();
                    OnQueueGenerationRequested?.Invoke();
                    return;
                }

                if (evt.keyCode == KeyCode.Tab && evt.shiftKey)
                {
                    evt.StopImmediatePropagation();
                    evt.StopPropagation();
                    OnNavigateBackRequested?.Invoke();
                    return;
                }

                if (evt.keyCode == KeyCode.Tab && !evt.shiftKey)
                {
                    evt.StopImmediatePropagation();
                    evt.StopPropagation();
                    OnNavigateForwardRequested?.Invoke();
                    return;
                }

                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    if (!CommitPolycountFromField(clamp: true))
                    {
                        _polycountField.value = _getPolycount().ToString();
                    }
                    UpdateSummary();
                    _polycountField.Blur();

                    OnNavigateForwardRequested?.Invoke();
                    evt.StopPropagation();
                }
            }, TrickleDown.TrickleDown);

            _polycountField.RegisterValueChangedCallback(_ => UpdateSummary());
            _polycountField.RegisterCallback<FocusOutEvent>(_ =>
            {
                if (!CommitPolycountFromField(clamp: true))
                {
                    _polycountField.value = _getPolycount().ToString();
                }
                UpdateSummary();
            });
            _polycountField.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                CommitPolycountFromField(clamp: true);
            });
        }

        private void SetupLODsToggle()
        {
            _lodsToggle.RegisterValueChangedCallback(evt =>
            {
                _setLODs(evt.newValue);
                SavePrefBool(PrefLODs, evt.newValue);
                UpdateSummary();
            });
        }

        private void SetupColliderToggle()
        {
            _colliderToggle.RegisterValueChangedCallback(evt =>
            {
                _setCollider(evt.newValue);
                SavePrefBool(PrefCollider, evt.newValue);
                UpdateSummary();
            });
        }

        private void SetupTextureResDropdown()
        {
            _textureResDropdown.menu.MenuItems().Clear();
            string[] resolutions = { "256", "512", "1024", "2048" };
            foreach (var res in resolutions)
            {
                _textureResDropdown.menu.AppendAction(res, _ =>
                {
                    _textureResDropdown.text = res;
                    _setTexRes(res);
                    SavePrefString(PrefTextureRes, res);
                    UpdateSummary();
                });
            }
        }

        private void SetupAOToggle()
        {
            if (_aoToggle == null)
            {
                return;
            }

            _aoToggle.RegisterValueChangedCallback(evt =>
            {
                _setAO?.Invoke(evt.newValue);
                if (_setAO != null)
                {
                    SavePrefBool(PrefAO, evt.newValue);
                }
                UpdateSummary();
            });
        }

        private void SetupTransparentToggle()
        {
            if (_transparentToggle == null)
            {
                return;
            }

            _transparentToggle.RegisterValueChangedCallback(evt =>
            {
                _setTransparent?.Invoke(evt.newValue);
                if (_setTransparent != null)
                {
                    SavePrefBool(PrefTransparent, evt.newValue);
                }
                UpdateSummary();
            });
        }

        private void SetupAspectRatioDropdown()
        {
            if (_aspectRatioDropdown == null)
            {
                return;
            }

            RebuildAspectRatioDropdownChoices();
        }

        private void RebuildAspectRatioDropdownChoices()
        {
            if (_aspectRatioDropdown == null)
            {
                return;
            }

            _aspectRatioDropdown.menu.MenuItems().Clear();

            foreach (string aspectRatio in GetAspectRatioChoices())
            {
                _aspectRatioDropdown.menu.AppendAction(aspectRatio, _ =>
                {
                    _aspectRatioDropdown.text = aspectRatio;
                    _setAspectRatio?.Invoke(aspectRatio);
                    SavePrefString(PrefAspectRatio, aspectRatio);
                    UpdateSummary();
                });
            }
        }

        private void LoadValues()
        {
            _pivotDropdown.text = _getPivot();
            _topologyDropdown.text = GetEffectiveTopology();
            _polycountField.value = _getPolycount().ToString();
            _lodsToggle.SetValueWithoutNotify(_getLODs());
            _colliderToggle.SetValueWithoutNotify(_getCollider());
            _textureResDropdown.text = _getTexRes();
            if (_aspectRatioDropdown != null)
            {
                string aspectRatio = _getAspectRatio != null ? _getAspectRatio() : DefaultAspectRatio;
                _aspectRatioDropdown.text = string.IsNullOrWhiteSpace(aspectRatio) ? DefaultAspectRatio : aspectRatio;
            }
            if (_getAO != null)
            {
                _aoToggle.SetValueWithoutNotify(_getAO());
            }

            if (_getTransparent != null && _transparentToggle != null)
            {
                _transparentToggle.SetValueWithoutNotify(_getTransparent());
            }
        }

        private void ValidatePolycountLimits()
        {
            CommitPolycountFromField(clamp: true);
        }

        private void UpdateSummary()
        {
            string texRes = _getTexRes();
            string aspectRatio = _getAspectRatio != null ? _getAspectRatio() : null;
            string aspectRatioSummary = IsAspectRatioSelectionAllowed() && !string.IsNullOrWhiteSpace(aspectRatio)
                ? $" | AR {aspectRatio}"
                : string.Empty;
            bool transparent = GetTransparentValue();
            string transparentSummary = IsTransparentSettingVisible()
                ? $" | Transparent {(transparent ? "✓" : "✗")}"
                : string.Empty;
            if (!IsMeshSettingsVisible())
            {
                _foldout.text = $"Quality Settings: {texRes}px{aspectRatioSummary}{transparentSummary}";
                return;
            }

            bool ao = _getAO != null ? _getAO() : _aoToggle.value;
            string aoSymbol = ao ? "✓" : "✗";
            string pivot = _getPivot();
            string topology = GetEffectiveTopology();
            int polycount = GetPolycountForSummary();
            bool lods = _getLODs();
            bool collider = _getCollider();
            string polycountFormatted = polycount >= 1000
                ? $"{polycount / 1000}K"
                : polycount.ToString();
            string lodsSymbol = lods ? "✓" : "✗";
            string colliderSymbol = collider ? "✓" : "✗";

            _foldout.text = $"Quality Settings: Pivot: {pivot} | {topology} | {polycountFormatted} polys | LODs {lodsSymbol} | Collider {colliderSymbol} | {texRes}px{aspectRatioSummary} | AO {aoSymbol}{transparentSummary}";
        }

        private int GetPolycountForSummary()
        {
            if (TryGetPolycountFieldValue(out int value))
            {
                return value;
            }

            return _getPolycount();
        }

        private bool CommitPolycountFromField(bool clamp)
        {
            if (!TryGetPolycountFieldValue(out int value))
            {
                return false;
            }

            if (clamp)
            {
                int minValue = 1000;
                int maxValue = GetEffectiveTopology() == "Triangles" ? 1000000 : 200000;
                value = Mathf.Clamp(value, minValue, maxValue);
                _polycountField.value = value.ToString();
            }

            _setPolycount(value);
            SavePrefInt(PrefPolycount, value);
            return true;
        }

        private bool TryGetPolycountFieldValue(out int value)
        {
            value = 0;
            return _polycountField != null && int.TryParse(_polycountField.value, out value);
        }

        private void ApplyEditorPrefsToSettings()
        {
            if (string.IsNullOrEmpty(_prefsKeyPrefix))
            {
                return;
            }

            if (EditorPrefs.HasKey(GetPrefKey(PrefPivot)))
            {
                _setPivot(EditorPrefs.GetString(GetPrefKey(PrefPivot)));
            }

            if (IsTopologySelectionAllowed() && EditorPrefs.HasKey(GetPrefKey(PrefTopology)))
            {
                _setTopology(EditorPrefs.GetString(GetPrefKey(PrefTopology)));
            }
            else if (IsTopologySelectionAllowed())
            {
                // Decor topology defaults to quads when no explicit preference was saved yet.
                _setTopology(HardcodedTopology);
            }

            if (EditorPrefs.HasKey(GetPrefKey(PrefLODs)))
            {
                _setLODs(EditorPrefs.GetBool(GetPrefKey(PrefLODs)));
            }

            if (EditorPrefs.HasKey(GetPrefKey(PrefCollider)))
            {
                _setCollider(EditorPrefs.GetBool(GetPrefKey(PrefCollider)));
            }

            if (EditorPrefs.HasKey(GetPrefKey(PrefTextureRes)))
            {
                _setTexRes(EditorPrefs.GetString(GetPrefKey(PrefTextureRes)));
            }

            if (_setAspectRatio != null && EditorPrefs.HasKey(GetPrefKey(PrefAspectRatio)))
            {
                _setAspectRatio(EditorPrefs.GetString(GetPrefKey(PrefAspectRatio)));
            }

            if (EditorPrefs.HasKey(GetPrefKey(PrefPolycount)))
            {
                _setPolycount(EditorPrefs.GetInt(GetPrefKey(PrefPolycount)));
            }

            if (_setAO != null && EditorPrefs.HasKey(GetPrefKey(PrefAO)))
            {
                _setAO(EditorPrefs.GetBool(GetPrefKey(PrefAO)));
            }

            if (_setTransparent != null && EditorPrefs.HasKey(GetPrefKey(PrefTransparent)))
            {
                _setTransparent(EditorPrefs.GetBool(GetPrefKey(PrefTransparent)));
            }
        }

        private bool IsTopologySelectionAllowed()
        {
            return _allowTopologySelection?.Invoke() ?? true;
        }

        private bool IsAspectRatioSelectionAllowed()
        {
            if (_getAspectRatio == null || _setAspectRatio == null)
            {
                return false;
            }

            return _allowAspectRatioSelection?.Invoke() ?? false;
        }

        private bool IsMeshSettingsVisible()
        {
            return _showMeshSettings?.Invoke() ?? true;
        }

        private bool IsTransparentSettingVisible()
        {
            return _showTransparentSetting?.Invoke() ?? false;
        }

        private bool GetTransparentValue()
        {
            if (_getTransparent != null)
            {
                return _getTransparent();
            }

            return _transparentToggle?.value ?? false;
        }

        private List<string> GetAspectRatioChoices()
        {
            var configured = _getAspectRatioChoices?.Invoke();
            var choices = new List<string>();

            if (configured != null)
            {
                foreach (string choice in configured)
                {
                    if (string.IsNullOrWhiteSpace(choice) || ContainsIgnoreCase(choices, choice))
                    {
                        continue;
                    }

                    choices.Add(choice.Trim());
                }
            }

            if (choices.Count > 0)
            {
                return choices;
            }

            choices.AddRange(DefaultAspectRatios);
            return choices;
        }

        private string GetEffectiveTopology()
        {
            if (!IsTopologySelectionAllowed())
            {
                return HardcodedTopology;
            }

            return _getTopology != null ? _getTopology() : HardcodedTopology;
        }

        private void RestoreTopologyFromPrefs()
        {
            if (string.IsNullOrEmpty(_prefsKeyPrefix) || _setTopology == null)
            {
                return;
            }

            if (EditorPrefs.HasKey(GetPrefKey(PrefTopology)))
            {
                _setTopology(EditorPrefs.GetString(GetPrefKey(PrefTopology)));
            }
        }

        private void UpdateTopologySelection()
        {
            bool selectionAllowed = IsTopologySelectionAllowed();
            if (selectionAllowed && !_topologySelectionEnabled)
            {
                RestoreTopologyFromPrefs();
            }

            _topologySelectionEnabled = selectionAllowed;

            if (_topologyColumn != null)
            {
                _topologyColumn.style.display = selectionAllowed && IsMeshSettingsVisible() ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (!selectionAllowed)
            {
                _setTopology?.Invoke(HardcodedTopology);
            }

            if (_topologyDropdown != null)
            {
                _topologyDropdown.text = GetEffectiveTopology();
            }

            ValidatePolycountLimits();
        }

        private void UpdateMeshSettingsVisibility()
        {
            bool visible = IsMeshSettingsVisible();

            if (_pivotColumn != null)
            {
                _pivotColumn.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (_polycountColumn != null)
            {
                _polycountColumn.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (_lodsColumn != null)
            {
                _lodsColumn.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (_colliderColumn != null)
            {
                _colliderColumn.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (_aoColumn != null)
            {
                _aoColumn.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void UpdateTransparentSettingVisibility()
        {
            bool visible = IsTransparentSettingVisible();

            if (_transparentColumn != null)
            {
                _transparentColumn.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (_transparentToggle == null)
            {
                return;
            }

            if (!visible)
            {
                _transparentToggle.SetEnabled(true);
                return;
            }

            _transparentToggle.SetEnabled(true);
            if (_getTransparent != null)
            {
                _transparentToggle.SetValueWithoutNotify(_getTransparent());
            }
        }

        private void UpdateAspectRatioSelection()
        {
            bool selectionAllowed = IsAspectRatioSelectionAllowed();

            if (_aspectRatioColumn != null)
            {
                _aspectRatioColumn.style.display = selectionAllowed ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (!selectionAllowed || _aspectRatioDropdown == null)
            {
                return;
            }

            var choices = GetAspectRatioChoices();
            RebuildAspectRatioDropdownChoices();

            string aspectRatio = _getAspectRatio != null ? _getAspectRatio() : null;
            if (string.IsNullOrWhiteSpace(aspectRatio) || !ContainsIgnoreCase(choices, aspectRatio))
            {
                aspectRatio = choices.Count > 0 ? choices[0] : DefaultAspectRatio;
                _setAspectRatio?.Invoke(aspectRatio);
            }

            _aspectRatioDropdown.text = aspectRatio;
        }

        private static bool ContainsIgnoreCase(List<string> values, string value)
        {
            if (values == null || string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            foreach (string item in values)
            {
                if (string.Equals(item, value, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private string GetPrefKey(string suffix)
        {
            return $"{_prefsKeyPrefix}.{suffix}";
        }

        private void SavePrefString(string suffix, string value)
        {
            if (string.IsNullOrEmpty(_prefsKeyPrefix))
            {
                return;
            }

            EditorPrefs.SetString(GetPrefKey(suffix), value ?? "");
        }

        private void SavePrefInt(string suffix, int value)
        {
            if (string.IsNullOrEmpty(_prefsKeyPrefix))
            {
                return;
            }

            EditorPrefs.SetInt(GetPrefKey(suffix), value);
        }

        private void SavePrefBool(string suffix, bool value)
        {
            if (string.IsNullOrEmpty(_prefsKeyPrefix))
            {
                return;
            }

            EditorPrefs.SetBool(GetPrefKey(suffix), value);
        }
    }
}
