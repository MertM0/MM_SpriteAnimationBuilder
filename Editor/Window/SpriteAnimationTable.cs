using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MM.SpriteAnimation
{
    internal sealed class SpriteAnimationTable
    {
        private const string NameColumn = "name";
        private const string FramesColumn = "frames";
        private const string FrameRateColumn = "fps";
        private const string LoopColumn = "loop";
        private const string LengthColumn = "length";
        private const string StatusColumn = "status";
        private const string ActionsColumn = "actions";
        private const string BuildButtonName = "build";
        private const string StatusClassPrefix = "sab-status--";
        private const string Missing = "\u2014";

        private static readonly AnimationStatus[] Statuses = (AnimationStatus[])Enum.GetValues(typeof(AnimationStatus));
        private static readonly string[] StatusClasses = Statuses.Select(status => StatusClassPrefix + status.ToString().ToLowerInvariant()).ToArray();

        private readonly MultiColumnListView view;
        private readonly List<AnimationSource> rows = new List<AnimationSource>();
        private SpriteAnimationProfile profile;
        private string rowsSignature;

        internal SpriteAnimationTable()
        {
            view = new MultiColumnListView
            {
                itemsSource = rows,
                selectionType = SelectionType.Multiple,
                sortingMode = ColumnSortingMode.Custom,
                showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly,
                fixedItemHeight = 22f,
            };
            view.AddToClassList("sab-table");
            view.viewDataKey = "sab-table";
            view.columns.Add(TextColumn(NameColumn, "Animation", 240f, source => source.Name, true));
            view.columns.Add(TextColumn(FramesColumn, "Frames", 80f, FrameSummary, false));
            view.columns.Add(FrameRateColumnDefinition());
            view.columns.Add(LoopColumnDefinition());
            view.columns.Add(TextColumn(LengthColumn, "Length", 70f, LengthText, false));
            view.columns.Add(StatusColumnDefinition());
            view.columns.Add(ActionsColumnDefinition());
            view.columnSortingChanged += () => Reload(null);
            view.selectionChanged += _ => SelectionChanged?.Invoke(Selected);
        }

        internal event Action<IReadOnlyList<AnimationSource>> SelectionChanged;
        internal event Action<AnimationSource> BuildRequested;
        internal event Action<AnimationSource> ValidateRequested;
        internal event Action<AnimationSource> ResetTimingRequested;
        internal event Action<AnimationSource, int, bool> RowSettingsChanged;

        internal VisualElement View => view;
        internal IReadOnlyList<AnimationSource> Selected => view.selectedItems.Cast<AnimationSource>().ToList();

        internal void SetRows(SpriteAnimationProfile owner, IEnumerable<AnimationSource> sources)
        {
            List<AnimationSource> incoming = sources.ToList();
            string signature = Signature(owner, incoming);
            if (owner == profile && signature == rowsSignature) return;

            profile = owner;
            rowsSignature = signature;
            Reload(() =>
            {
                rows.Clear();
                rows.AddRange(incoming);
            });
        }

        private static string Signature(SpriteAnimationProfile owner, List<AnimationSource> sources)
        {
            if (owner == null) return string.Empty;

            var text = new StringBuilder();
            foreach (AnimationSource source in sources)
            {
                text.Append(source.Name).Append('|').Append((int)source.Status).Append('|').Append(source.Fingerprint)
                    .Append('|').Append(owner.FrameRateOf(source.Name)).Append('|').Append(owner.LoopOf(source.Name))
                    .Append('|').Append(FrameSummary(source)).Append('|').Append(LengthText(source)).Append('|').Append(source.SuggestedState)
                    .Append('|').Append(Details(source)).Append('\n');
            }
            return text.ToString();
        }

        internal void Select(string name)
        {
            int index = rows.FindIndex(row => row.Name == name);
            if (index < 0) return;
            view.SetSelection(index);
            view.ScrollToItem(index);
        }

        internal static string FrameSummary(AnimationSource source)
        {
            if (!source.HasFolder) return Missing;
            IEnumerable<int> counts = source.Parts.Select(part => part.Frames.Count(frame => frame != null));
            if (source.ReferenceFrames != null) counts = counts.Append(source.ReferenceFrames.Count);
            return string.Join("·", counts);
        }

        internal static string LengthText(AnimationSource source)
        {
            return source.Timing != null ? source.Timing.Length.ToString("0.00", CultureInfo.InvariantCulture) + " s" : Missing;
        }

        internal static string StatusText(AnimationSource source)
        {
            switch (source.Status)
            {
                case AnimationStatus.NoArt:
                    return "No art";
                case AnimationStatus.NoState:
                    return source.SuggestedState != null ? $"No state (did you mean '{source.SuggestedState}'?)" : "No state in controller";
                case AnimationStatus.Invalid:
                    return source.Problems.Count > 1 ? $"{source.Problems[0]} (+{source.Problems.Count - 1} more)" : source.Problems[0];
                case AnimationStatus.TimingReset:
                    return "Timing reset needed";
                case AnimationStatus.New:
                    return WithWarnings(source.CreatesState ? "New (state will be created)" : "New", source);
                case AnimationStatus.Changed:
                    return WithWarnings(WithTiming("Changed", source), source);
                default:
                    return WithWarnings(WithTiming("Up to date", source), source);
            }
        }

        internal static bool HasCustomTiming(AnimationSource source) => source.Timing != null && !source.Timing.IsUniform;

        private static string WithTiming(string text, AnimationSource source) => HasCustomTiming(source) ? $"{text}, custom timing" : text;

        internal static string Details(AnimationSource source)
        {
            IEnumerable<string> lines = source.Problems.Concat(source.Warnings);
            if (source.TimingProblem != null) lines = lines.Prepend(source.TimingProblem);
            return string.Join("\n", lines);
        }

        private static string WithWarnings(string text, AnimationSource source)
        {
            int count = source.Warnings.Count;
            return count == 0 ? text : $"{text} ({count} warning{(count == 1 ? string.Empty : "s")})";
        }

        private void Reload(Action replaceRows)
        {
            var selectedNames = new HashSet<string>(view.selectedItems.Cast<AnimationSource>().Select(source => source.Name), StringComparer.Ordinal);
            replaceRows?.Invoke();
            SortRows();
            view.Rebuild();

            var indices = new List<int>();
            for (int i = 0; i < rows.Count; i++)
            {
                if (selectedNames.Contains(rows[i].Name)) indices.Add(i);
            }
            view.SetSelectionWithoutNotify(indices);
            SelectionChanged?.Invoke(Selected);
        }

        private void SortRows()
        {
            SortColumnDescription sort = view.sortedColumns.FirstOrDefault();
            if (sort == null || profile == null) return;

            Comparison<AnimationSource> compare = Comparer(sort.columnName);
            int sign = sort.direction == SortDirection.Ascending ? 1 : -1;
            rows.Sort((a, b) =>
            {
                int result = compare(a, b);
                if (result == 0) result = string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                return sign * result;
            });
        }

        private Comparison<AnimationSource> Comparer(string column)
        {
            switch (column)
            {
                case FramesColumn: return (a, b) => a.FrameCount.CompareTo(b.FrameCount);
                case FrameRateColumn: return (a, b) => profile.FrameRateOf(a.Name).CompareTo(profile.FrameRateOf(b.Name));
                case LoopColumn: return (a, b) => profile.LoopOf(a.Name).CompareTo(profile.LoopOf(b.Name));
                case LengthColumn: return (a, b) => (a.Timing?.Length ?? -1f).CompareTo(b.Timing?.Length ?? -1f);
                case StatusColumn: return (a, b) => a.Status.CompareTo(b.Status);
                default: return (a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            }
        }

        private Column TextColumn(string id, string title, float width, Func<AnimationSource, string> text, bool stretchable)
        {
            return new Column
            {
                name = id,
                title = title,
                width = width,
                minWidth = 60f,
                stretchable = stretchable,
                makeCell = MakeLabelCell,
                bindCell = (element, index) => ((Label)element).text = text(rows[index]),
            };
        }

        private static VisualElement MakeLabelCell()
        {
            var label = new Label();
            label.AddToClassList("sab-cell");
            return label;
        }

        private Column FrameRateColumnDefinition()
        {
            return new Column
            {
                name = FrameRateColumn,
                title = "FPS",
                width = 60f,
                makeCell = () =>
                {
                    var field = new IntegerField { isDelayed = true };
                    field.RegisterValueChangedCallback(evt =>
                    {
                        if (!(field.userData is AnimationSource source)) return;
                        int frameRate = Mathf.Max(1, evt.newValue);
                        if (frameRate != evt.newValue) field.SetValueWithoutNotify(frameRate);
                        RowSettingsChanged?.Invoke(source, frameRate, profile.LoopOf(source.Name));
                    });
                    return field;
                },
                bindCell = (element, index) =>
                {
                    var field = (IntegerField)element;
                    AnimationSource source = rows[index];
                    field.userData = source;
                    field.SetValueWithoutNotify(profile.FrameRateOf(source.Name));
                    field.style.visibility = source.HasFolder ? Visibility.Visible : Visibility.Hidden;
                },
            };
        }

        private Column LoopColumnDefinition()
        {
            return new Column
            {
                name = LoopColumn,
                title = "Loop",
                width = 45f,
                makeCell = () =>
                {
                    var toggle = new Toggle();
                    toggle.RegisterValueChangedCallback(evt =>
                    {
                        if (toggle.userData is AnimationSource source) RowSettingsChanged?.Invoke(source, profile.FrameRateOf(source.Name), evt.newValue);
                    });
                    return toggle;
                },
                bindCell = (element, index) =>
                {
                    var toggle = (Toggle)element;
                    AnimationSource source = rows[index];
                    toggle.userData = source;
                    toggle.SetValueWithoutNotify(profile.LoopOf(source.Name));
                    toggle.style.visibility = source.HasFolder ? Visibility.Visible : Visibility.Hidden;
                },
            };
        }

        private Column StatusColumnDefinition()
        {
            return new Column
            {
                name = StatusColumn,
                title = "Status",
                width = 200f,
                minWidth = 90f,
                makeCell = MakeLabelCell,
                bindCell = (element, index) =>
                {
                    var label = (Label)element;
                    AnimationSource source = rows[index];
                    label.text = StatusText(source);
                    label.tooltip = Details(source);
                    for (int i = 0; i < Statuses.Length; i++) label.EnableInClassList(StatusClasses[i], Statuses[i] == source.Status);
                },
            };
        }

        private Column ActionsColumnDefinition()
        {
            return new Column
            {
                name = ActionsColumn,
                title = string.Empty,
                width = 96f,
                minWidth = 96f,
                sortable = false,
                makeCell = () =>
                {
                    var cell = new VisualElement();
                    cell.AddToClassList("sab-row-actions");
                    var build = new Button(() =>
                    {
                        if (cell.userData is AnimationSource source) BuildRequested?.Invoke(source);
                    }) { text = "Build", name = BuildButtonName };
                    var more = new Button(() =>
                    {
                        if (cell.userData is AnimationSource source) ShowRowMenu(source);
                    }) { text = "...", tooltip = "More actions" };
                    cell.Add(build);
                    cell.Add(more);
                    return cell;
                },
                bindCell = (element, index) =>
                {
                    element.userData = rows[index];
                    element.Q<Button>(BuildButtonName).SetEnabled(rows[index].IsBuildable);
                },
            };
        }

        private void ShowRowMenu(AnimationSource source)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(profile.ClipPath(source.Name));
            var menu = new GenericMenu();
            AddMenuItem(menu, "Show Sprite Folder", source.HasFolder, () => EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<DefaultAsset>(source.Folder)));
            AddMenuItem(menu, "Select Clip", clip != null, () => Selection.activeObject = clip);
            AddMenuItem(menu, "Open Clip in Animation Window", clip != null, () =>
            {
                Selection.activeObject = clip;
                EditorApplication.ExecuteMenuItem("Window/Animation/Animation");
            });
            menu.AddSeparator(string.Empty);
            bool canReset = source.Status == AnimationStatus.TimingReset || (source.IsBuildable && HasCustomTiming(source));
            AddMenuItem(menu, "Reset Timing and Build", canReset, () => ResetTimingRequested?.Invoke(source));
            AddMenuItem(menu, "Validate", source.HasFolder, () => ValidateRequested?.Invoke(source));
            menu.ShowAsContext();
        }

        private static void AddMenuItem(GenericMenu menu, string label, bool enabled, GenericMenu.MenuFunction action)
        {
            if (enabled) menu.AddItem(new GUIContent(label), false, action);
            else menu.AddDisabledItem(new GUIContent(label));
        }
    }
}
