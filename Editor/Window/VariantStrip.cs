using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace MM.SpriteAnimation
{
    internal sealed class VariantStrip : Toolbar
    {
        private readonly Action<SpriteAnimationProfile> selected;
        private readonly Action<SpriteAnimationProfile> addVariant;
        private readonly Action<bool> overviewToggled;

        private string signature;

        internal VariantStrip(Action<SpriteAnimationProfile> selected, Action<SpriteAnimationProfile> addVariant, Action<bool> overviewToggled)
        {
            this.selected = selected;
            this.addVariant = addVariant;
            this.overviewToggled = overviewToggled;
            AddToClassList("sab-variant-strip");
            style.display = DisplayStyle.None;
        }

        internal void SetFamily(IReadOnlyList<SpriteAnimationProfile> family, SpriteAnimationProfile current, bool overviewVisible)
        {
            if (family.Count < 2)
            {
                style.display = DisplayStyle.None;
                signature = null;
                return;
            }

            style.display = DisplayStyle.Flex;
            string next = Signature(family, current, overviewVisible);
            if (next == signature) return;
            signature = next;
            Rebuild(family, current, overviewVisible);
        }

        private void Rebuild(IReadOnlyList<SpriteAnimationProfile> family, SpriteAnimationProfile current, bool overviewVisible)
        {
            Clear();
            foreach (SpriteAnimationProfile member in family)
            {
                var toggle = new ToolbarToggle { text = member.DisplayName, value = member == current };
                toggle.RegisterValueChangedCallback(evt =>
                {
                    if (evt.newValue) schedule.Execute(() => selected(member));
                    else toggle.SetValueWithoutNotify(true);
                });
                Add(toggle);
            }

            SpriteAnimationProfile baseProfile = family[0];
            var add = new ToolbarButton(() => addVariant(baseProfile)) { text = "+", tooltip = "New variant" };
            Add(add);

            var spacer = new ToolbarSpacer();
            spacer.style.flexGrow = 1f;
            Add(spacer);

            var overview = new ToolbarToggle { text = "Overview", value = overviewVisible };
            overview.RegisterValueChangedCallback(evt =>
            {
                bool visible = evt.newValue;
                schedule.Execute(() => overviewToggled(visible));
            });
            Add(overview);
        }

        private static string Signature(IReadOnlyList<SpriteAnimationProfile> family, SpriteAnimationProfile current, bool overviewVisible)
        {
            IEnumerable<string> members = family.Select(member => $"{ProfileTabBar.GuidOf(member)}:{member.DisplayName}");
            return $"{string.Join("|", members)}#{ProfileTabBar.GuidOf(current)}#{overviewVisible}";
        }
    }
}
