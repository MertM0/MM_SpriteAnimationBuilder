using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MM.SpriteAnimation
{
    public sealed class SpriteAnimationWindow : EditorWindow
    {
        private const string WindowTitle = "Sprite Animation Builder";
        private const string StyleSheetFile = "SpriteAnimationWindow.uss";
        private const int MaxLogEntries = 300;
        private const long SettingsRefreshDelay = 150;

        private readonly List<ReportEntry> log = new List<ReportEntry>();

        [SerializeField] private SpriteAnimationProfile profile;
        [SerializeField] private bool settingsVisible;

        private SpriteAnimationScan scan;
        private SerializedObject settingsObject;
        private IVisualElementScheduledItem refreshItem;

        private ProfileTabBar tabBar;
        private ScrollView settingsPanel;
        private ToolbarSearchField searchField;
        private EnumField statusFilter;
        private Label profileMessage;
        private SpriteAnimationTable table;
        private SpriteAnimationPreview preview;
        private Label details;
        private ListView logView;

        [MenuItem("Tools/Sprite Animation Builder")]
        public static void Open() => GetWindow<SpriteAnimationWindow>(WindowTitle);

        public static void Open(SpriteAnimationProfile target)
        {
            SpriteAnimationWindow window = GetWindow<SpriteAnimationWindow>(WindowTitle);
            window.SelectProfile(target);
        }

        private void OnEnable()
        {
            EditorApplication.projectChanged += QueueRefresh;
            Undo.undoRedoPerformed += QueueRefresh;
        }

        private void OnDisable()
        {
            EditorApplication.projectChanged -= QueueRefresh;
            Undo.undoRedoPerformed -= QueueRefresh;
            DisposeSettingsObject();
        }

        private void OnFocus() => QueueRefresh();

        private void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            StyleSheet styleSheet = LoadStyleSheet();
            if (styleSheet != null) root.styleSheets.Add(styleSheet);
            root.AddToClassList("sab-root");

            tabBar = new ProfileTabBar(SelectProfile, OnProfileCreated, OnSettingsToggled);
            root.Add(tabBar);

            settingsPanel = new ScrollView();
            settingsPanel.AddToClassList("sab-settings");
            root.Add(settingsPanel);

            root.Add(CreateFilterBar());

            profileMessage = new Label();
            profileMessage.AddToClassList("sab-profile-message");
            root.Add(profileMessage);

            table = new SpriteAnimationTable();
            table.SelectionChanged += OnSelectionChanged;
            table.BuildRequested += source => Run(new[] { source.Name }, false);
            table.ValidateRequested += source => Run(new[] { source.Name }, true);
            table.ResetTimingRequested += source => Run(new[] { source.Name }, false, true);
            table.RowSettingsChanged += OnRowSettingsChanged;

            var upper = new VisualElement();
            upper.AddToClassList("sab-upper");
            upper.Add(table.View);
            upper.Add(CreateActionBar());

            preview = new SpriteAnimationPreview();
            var lower = new TwoPaneSplitView(1, 300f, TwoPaneSplitViewOrientation.Horizontal) { viewDataKey = "sab-split-lower" };
            lower.Add(preview);
            lower.Add(CreateLogPanel());

            var split = new TwoPaneSplitView(1, 280f, TwoPaneSplitViewOrientation.Vertical) { viewDataKey = "sab-split-main" };
            split.AddToClassList("sab-split");
            split.Add(upper);
            split.Add(lower);
            root.Add(split);

            refreshItem = root.schedule.Execute(RefreshAll);
            refreshItem.Pause();
            RefreshAll();
        }

        private VisualElement CreateFilterBar()
        {
            var bar = new Toolbar();
            searchField = new ToolbarSearchField();
            searchField.RegisterValueChangedCallback(_ => ApplyFilter());
            statusFilter = new EnumField(StatusFilter.All);
            statusFilter.AddToClassList("sab-filter");
            statusFilter.RegisterValueChangedCallback(_ => ApplyFilter());
            var spacer = new ToolbarSpacer();
            spacer.style.flexGrow = 1f;

            bar.Add(searchField);
            bar.Add(statusFilter);
            bar.Add(spacer);
            bar.Add(new ToolbarButton(RefreshAll) { text = "Refresh" });
            return bar;
        }

        private VisualElement CreateActionBar()
        {
            var bar = new VisualElement();
            bar.AddToClassList("sab-actions");
            bar.Add(new Button(() => Run(SelectedNames(), true)) { text = "Validate Selected" });
            bar.Add(new Button(() => Run(SelectedNames(), false)) { text = "Build Selected" });
            bar.Add(new Button(() => Run(Names(source => source.NeedsBuild), false)) { text = "Build Ready" });
            bar.Add(new Button(() => Run(Names(source => source.InBuildAll), false)) { text = "Build All" });
            return bar;
        }

        private VisualElement CreateLogPanel()
        {
            var panel = new VisualElement();
            panel.AddToClassList("sab-log-panel");

            details = new Label();
            details.AddToClassList("sab-details");
            panel.Add(details);

            var header = new Toolbar();
            header.Add(new Label("Log"));
            var spacer = new ToolbarSpacer();
            spacer.style.flexGrow = 1f;
            header.Add(spacer);
            header.Add(new ToolbarButton(ClearLog) { text = "Clear" });
            panel.Add(header);

            logView = new ListView(log, 18f, MakeLogRow, BindLogRow) { selectionType = SelectionType.Single };
            logView.AddToClassList("sab-log");
            logView.selectionChanged += OnLogSelected;
            panel.Add(logView);
            return panel;
        }

        private static VisualElement MakeLogRow()
        {
            var label = new Label();
            label.AddToClassList("sab-log-row");
            return label;
        }

        private void BindLogRow(VisualElement element, int index)
        {
            var label = (Label)element;
            ReportEntry entry = log[index];
            label.text = $"{entry.Time.ToString("HH:mm:ss", CultureInfo.InvariantCulture)}  {entry}";
            label.tooltip = entry.Message;
            label.EnableInClassList("sab-log--error", entry.Severity == ReportSeverity.Error);
            label.EnableInClassList("sab-log--warning", entry.Severity == ReportSeverity.Warning);
        }

        private void OnLogSelected(IEnumerable<object> selection)
        {
            if (selection.FirstOrDefault() is ReportEntry entry && entry.Animation != null) table.Select(entry.Animation);
        }

        private void ClearLog()
        {
            log.Clear();
            logView.RefreshItems();
        }

        private void AppendLog(SpriteAnimationReport report)
        {
            log.InsertRange(0, report.Entries);
            if (log.Count > MaxLogEntries) log.RemoveRange(MaxLogEntries, log.Count - MaxLogEntries);
            logView.RefreshItems();
            logView.ScrollToItem(0);
        }

        private void Run(IEnumerable<string> names, bool validateOnly, bool resetTiming = false)
        {
            if (profile == null) return;

            List<string> list = names.ToList();
            SpriteAnimationReport report;
            if (list.Count == 0)
            {
                report = new SpriteAnimationReport();
                report.Info(null, "Nothing to do: no matching animations.");
            }
            else
            {
                string title = validateOnly ? "Validating animations" : "Building animations";
                Action<string, float> progress = (info, value) => EditorUtility.DisplayProgressBar(title, info, value);
                try
                {
                    report = validateOnly ? SpriteAnimationBuilder.Validate(profile, list, progress) : SpriteAnimationBuilder.Build(profile, list, progress, resetTiming);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    report = new SpriteAnimationReport();
                    report.Error(null, $"{exception.GetType().Name}: {exception.Message} (stack trace in the Console)");
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                }
            }
            AppendLog(report);
            Refresh();
        }

        private IEnumerable<string> SelectedNames() => table.Selected.Where(source => source.HasFolder).Select(source => source.Name);

        private IEnumerable<string> Names(Func<AnimationSource, bool> predicate)
        {
            return scan == null ? Enumerable.Empty<string>() : scan.Sources.Where(predicate).Select(source => source.Name);
        }

        private void OnSelectionChanged(IReadOnlyList<AnimationSource> selected)
        {
            AnimationSource first = selected.Count > 0 ? selected[0] : null;
            preview.Show(profile, first);
            string text = first != null ? SpriteAnimationTable.Details(first) : string.Empty;
            details.text = text;
            details.style.display = text.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void OnRowSettingsChanged(AnimationSource source, int frameRate, bool loop)
        {
            if (profile == null) return;
            profile.SetRowSettings(source.Name, frameRate, loop);
            QueueRefresh();
        }

        private void QueueRefresh() => refreshItem?.ExecuteLater(0);

        private void RefreshAll()
        {
            ReloadProfiles();
            Refresh();
        }

        private void Refresh()
        {
            if (table == null) return;
            scan = profile != null ? SpriteAnimationScanner.Scan(profile) : null;
            ShowProfileMessage();
            ApplyFilter();
        }

        private void ShowProfileMessage()
        {
            var lines = new List<string>();
            if (profile == null) lines.Add(tabBar.Profiles.Count == 0 ? "No profiles yet. Press + to create one." : "Select a profile.");
            else
            {
                lines.AddRange(scan.ProfileErrors);
                lines.AddRange(scan.ProfileWarnings);
            }
            profileMessage.text = string.Join("\n", lines);
            profileMessage.style.display = lines.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void ApplyFilter()
        {
            if (table == null) return;

            IEnumerable<AnimationSource> sources = scan != null ? scan.Sources : Enumerable.Empty<AnimationSource>();
            string search = searchField.value?.Trim();
            if (!string.IsNullOrEmpty(search)) sources = sources.Where(source => source.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);

            var filter = (StatusFilter)statusFilter.value;
            if (filter != StatusFilter.All) sources = sources.Where(source => Matches(source, filter));
            table.SetRows(profile, sources);
        }

        private static bool Matches(AnimationSource source, StatusFilter filter)
        {
            switch (filter)
            {
                case StatusFilter.NeedsBuild: return source.NeedsBuild || source.Status == AnimationStatus.TimingReset;
                case StatusFilter.UpToDate: return source.Status == AnimationStatus.UpToDate;
                case StatusFilter.Invalid: return source.Status == AnimationStatus.Invalid;
                case StatusFilter.NoArt: return source.Status == AnimationStatus.NoArt;
                case StatusFilter.NoState: return source.Status == AnimationStatus.NoState;
                default: return true;
            }
        }

        private void SelectProfile(SpriteAnimationProfile target)
        {
            if (target == null) return;
            profile = target;
            BuilderPrefs.SelectedProfile = ProfileTabBar.GuidOf(target);
            if (table == null) return;
            RefreshAll();
        }

        private void OnProfileCreated(SpriteAnimationProfile created)
        {
            settingsVisible = true;
            SelectProfile(created);
        }

        private void OnSettingsToggled(bool visible)
        {
            settingsVisible = visible;
            RebuildSettings();
        }

        private void ReloadProfiles()
        {
            profile = tabBar.Reload(profile, settingsVisible, out bool rebuilt);
            if (rebuilt) RebuildSettings();
        }

        private void RebuildSettings()
        {
            settingsPanel.Clear();
            DisposeSettingsObject();
            bool show = settingsVisible && profile != null;
            settingsPanel.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show) return;

            settingsObject = new SerializedObject(profile);
            var inspector = new InspectorElement(profile);
            inspector.TrackSerializedObjectValue(settingsObject, _ => refreshItem?.ExecuteLater(SettingsRefreshDelay));
            settingsPanel.Add(inspector);
        }

        private void DisposeSettingsObject()
        {
            settingsObject?.Dispose();
            settingsObject = null;
        }

        private StyleSheet LoadStyleSheet()
        {
            string scriptPath = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this));
            string directory = Path.GetDirectoryName(scriptPath).Replace('\\', '/');
            return AssetDatabase.LoadAssetAtPath<StyleSheet>($"{directory}/{StyleSheetFile}");
        }
    }
}
