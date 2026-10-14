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
        private readonly List<SpriteAnimationProfile> logOwners = new List<SpriteAnimationProfile>();

        [SerializeField] private SpriteAnimationProfile profile;
        [SerializeField] private bool settingsVisible;
        [SerializeField] private bool overviewVisible;

        private SpriteAnimationScan scan;
        private SerializedObject settingsObject;
        private SpriteAnimationProfile settingsProfile;
        private IVisualElementScheduledItem refreshItem;

        private ProfileTabBar tabBar;
        private VariantStrip variantStrip;
        private VariantOverview overview;
        private VisualElement actionBar;
        private Button buildFamilyButton;
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

            variantStrip = new VariantStrip(SelectProfile, tabBar.CreateVariantOf, OnOverviewToggled);
            root.Add(variantStrip);

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
            actionBar = CreateActionBar();
            upper.Add(actionBar);
            overview = new VariantOverview();
            overview.CellClicked += OnOverviewCellClicked;
            upper.Add(overview);

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
            buildFamilyButton = new Button(RunFamily) { text = "Build Family" };
            buildFamilyButton.style.display = DisplayStyle.None;
            bar.Add(buildFamilyButton);
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
            int index = logView.selectedIndex;
            if (index < 0 || index >= log.Count) return;

            ReportEntry entry = log[index];
            SpriteAnimationProfile owner = logOwners[index];
            if (owner != null && owner != profile) SelectProfile(owner);
            if (entry.Animation != null) table.Select(entry.Animation);
        }

        private void ClearLog()
        {
            log.Clear();
            logOwners.Clear();
            logView.RefreshItems();
        }

        private void AppendLog(SpriteAnimationProfile owner, SpriteAnimationReport report)
        {
            log.InsertRange(0, report.Entries);
            logOwners.InsertRange(0, Enumerable.Repeat(owner, report.Entries.Count));
            if (log.Count > MaxLogEntries)
            {
                log.RemoveRange(MaxLogEntries, log.Count - MaxLogEntries);
                logOwners.RemoveRange(MaxLogEntries, logOwners.Count - MaxLogEntries);
            }
            logView.RefreshItems();
            logView.ScrollToItem(0);
        }

        private void RunFamily()
        {
            IReadOnlyList<SpriteAnimationProfile> family = tabBar.FamilyOf(profile);
            if (family.Count < 2) return;

            List<string> clashes = family.SelectMany(member => ProfileVariants.ClipClashes(member, family).Select(clash => $"{member.DisplayName}: {clash}")).ToList();
            if (clashes.Count > 0)
            {
                var stopped = new SpriteAnimationReport();
                foreach (string clash in clashes) stopped.Error(null, $"Build Family stopped. {clash}");
                AppendLog(profile, stopped);
                return;
            }

            try
            {
                for (int i = 0; i < family.Count; i++)
                {
                    SpriteAnimationProfile member = family[i];
                    int index = i;
                    Action<string, float> progress = (info, value) => EditorUtility.DisplayProgressBar("Building family", $"{member.DisplayName}: {info}", (index + value) / family.Count);
                    SpriteAnimationReport report;
                    try
                    {
                        report = SpriteAnimationBuilder.Build(member, null, progress);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                        report = new SpriteAnimationReport();
                        report.Error(null, $"{exception.GetType().Name}: {exception.Message} (stack trace in the Console)");
                    }
                    AppendLog(member, report);
                    var header = new SpriteAnimationReport();
                    header.Info(null, $"— {member.DisplayName} —");
                    AppendLog(member, header);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            Refresh();
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
            AppendLog(profile, report);
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

            IReadOnlyList<SpriteAnimationProfile> family = tabBar.FamilyOf(profile);
            variantStrip.SetFamily(family, profile, overviewVisible);
            buildFamilyButton.style.display = family.Count > 1 ? DisplayStyle.Flex : DisplayStyle.None;

            bool showOverview = overviewVisible && family.Count > 1;
            table.View.style.display = showOverview ? DisplayStyle.None : DisplayStyle.Flex;
            actionBar.style.display = showOverview ? DisplayStyle.None : DisplayStyle.Flex;
            overview.style.display = showOverview ? DisplayStyle.Flex : DisplayStyle.None;
            if (showOverview)
            {
                List<SpriteAnimationScan> scans = family.Select(member => member == profile ? scan : SpriteAnimationScanner.Scan(member)).ToList();
                overview.SetData(family, scans);
            }
        }

        private void ShowProfileMessage()
        {
            var lines = new List<string>();
            if (profile == null) lines.Add(tabBar.Profiles.Count == 0 ? "No profiles yet. Press + to create one." : "Select a profile.");
            else
            {
                lines.AddRange(scan.ProfileErrors);
                lines.AddRange(scan.ProfileWarnings);
                lines.AddRange(SiblingClashes());
            }
            profileMessage.text = string.Join("\n", lines);
            profileMessage.style.display = lines.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private IEnumerable<string> SiblingClashes()
        {
            return ProfileVariants.ClipClashes(profile, tabBar.FamilyOf(profile).Where(other => other != profile.VariantOf));
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
                case StatusFilter.Fallback: return source.IsFallback;
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

        private void OnOverviewToggled(bool visible)
        {
            overviewVisible = visible;
            Refresh();
        }

        private void OnOverviewCellClicked(SpriteAnimationProfile member, string name)
        {
            searchField.SetValueWithoutNotify(string.Empty);
            statusFilter.SetValueWithoutNotify(StatusFilter.All);
            overviewVisible = false;
            SelectProfile(member);
            table.Select(name);
        }

        private void ReloadProfiles()
        {
            profile = tabBar.Reload(profile, settingsVisible, out bool rebuilt);
            if (rebuilt || profile != settingsProfile) RebuildSettings();
        }

        private void RebuildSettings()
        {
            settingsProfile = profile;
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
