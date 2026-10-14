using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace MM.SpriteAnimation
{
    internal sealed class VariantOverview : VisualElement
    {
        private const string Missing = "—";

        private readonly ScrollView scroll;

        internal VariantOverview()
        {
            AddToClassList("sab-overview");
            scroll = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            scroll.style.flexGrow = 1f;
            Add(scroll);
        }

        internal event Action<SpriteAnimationProfile, string> CellClicked;

        internal void SetData(IReadOnlyList<SpriteAnimationProfile> members, IReadOnlyList<SpriteAnimationScan> scans)
        {
            scroll.Clear();

            VisualElement header = Row();
            header.Add(Cell(string.Empty, true));
            foreach (SpriteAnimationProfile member in members) header.Add(Cell(member.DisplayName, true));
            scroll.Add(header);

            IEnumerable<string> names = scans.SelectMany(scan => scan.Sources).Select(source => source.Name).Distinct(StringComparer.Ordinal).OrderBy(name => name, StringComparer.OrdinalIgnoreCase);
            foreach (string name in names)
            {
                VisualElement row = Row();
                row.Add(Cell(name, true));
                for (int i = 0; i < members.Count; i++) row.Add(StatusCell(members[i], scans[i], name));
                scroll.Add(row);
            }
        }

        private static string ShortStatus(AnimationStatus status)
        {
            switch (status)
            {
                case AnimationStatus.New: return "New";
                case AnimationStatus.Changed: return "Changed";
                case AnimationStatus.UpToDate: return "Up to date";
                case AnimationStatus.Invalid: return "Invalid";
                case AnimationStatus.NoArt: return "No art";
                case AnimationStatus.NoState: return "No state";
                case AnimationStatus.TimingReset: return "Timing reset";
                default: return status.ToString();
            }
        }

        private Label StatusCell(SpriteAnimationProfile member, SpriteAnimationScan scan, string name)
        {
            if (!scan.IsUsable)
            {
                Label error = Cell("Profile error", false);
                error.tooltip = string.Join("\n", scan.ProfileErrors);
                return error;
            }

            AnimationSource source = scan.Find(name);
            if (source == null) return Cell(Missing, false);

            Label cell = Cell(CellText(source), false);
            cell.tooltip = SpriteAnimationTable.Details(source);
            cell.AddToClassList(SpriteAnimationTable.StatusClass(source.Status));
            cell.RegisterCallback<ClickEvent>(_ => CellClicked?.Invoke(member, name));
            return cell;
        }

        private static string CellText(AnimationSource source)
        {
            string text = ShortStatus(source.Status);
            if (source.IsFallback) text += "  ↪ " + source.ArtName;
            if (source.PlaysBaseClip) text += " → base";
            return text;
        }

        private static VisualElement Row()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            return row;
        }

        private static Label Cell(string text, bool header)
        {
            var cell = new Label(text);
            cell.AddToClassList("sab-overview-cell");
            if (header) cell.AddToClassList("sab-overview-header");
            return cell;
        }
    }
}
