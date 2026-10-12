using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MM.SpriteAnimation
{
    internal sealed class ProfileTabBar : Toolbar
    {
        private static readonly string[] SearchRoots = { "Assets" };

        private readonly List<SpriteAnimationProfile> profiles = new List<SpriteAnimationProfile>();
        private readonly List<ToolbarToggle> tabs = new List<ToolbarToggle>();
        private readonly Action<SpriteAnimationProfile> selected;
        private readonly Action<SpriteAnimationProfile> created;
        private readonly Action<bool> settingsToggled;

        private SpriteAnimationProfile shown;
        private bool settingsVisible;

        internal ProfileTabBar(Action<SpriteAnimationProfile> selected, Action<SpriteAnimationProfile> created, Action<bool> settingsToggled)
        {
            this.selected = selected;
            this.created = created;
            this.settingsToggled = settingsToggled;
        }

        internal IReadOnlyList<SpriteAnimationProfile> Profiles => profiles;

        internal static string GuidOf(SpriteAnimationProfile item) => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(item));

        internal SpriteAnimationProfile Reload(SpriteAnimationProfile current, bool showSettings, out bool rebuilt)
        {
            List<SpriteAnimationProfile> found = FindProfiles();
            bool listChanged = !found.SequenceEqual(profiles);
            profiles.Clear();
            profiles.AddRange(found);
            if (current == null || !profiles.Contains(current)) current = RestoreSelection();

            rebuilt = childCount == 0 || listChanged || current != shown;
            if (rebuilt)
            {
                shown = current;
                settingsVisible = showSettings;
                Rebuild();
            }
            else
            {
                for (int i = 0; i < tabs.Count; i++) tabs[i].text = profiles[i].DisplayName;
            }
            return current;
        }

        private static List<SpriteAnimationProfile> FindProfiles()
        {
            var found = new List<SpriteAnimationProfile>();
            foreach (string guid in AssetDatabase.FindAssets($"t:{nameof(SpriteAnimationProfile)}", SearchRoots))
            {
                var item = AssetDatabase.LoadAssetAtPath<SpriteAnimationProfile>(AssetDatabase.GUIDToAssetPath(guid));
                if (item != null) found.Add(item);
            }
            return ProfileOrder.Sort(found, GuidOf, item => item.DisplayName, BuilderPrefs.TabOrder);
        }

        private SpriteAnimationProfile RestoreSelection()
        {
            string path = AssetDatabase.GUIDToAssetPath(BuilderPrefs.SelectedProfile);
            SpriteAnimationProfile stored = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<SpriteAnimationProfile>(path);
            return stored != null && profiles.Contains(stored) ? stored : profiles.FirstOrDefault();
        }

        private void Rebuild()
        {
            Clear();
            tabs.Clear();
            foreach (SpriteAnimationProfile item in profiles)
            {
                var tab = new ToolbarToggle { text = item.DisplayName, value = item == shown };
                tab.RegisterValueChangedCallback(evt =>
                {
                    if (evt.newValue) schedule.Execute(() => selected(item));
                    else tab.SetValueWithoutNotify(true);
                });
                tab.AddManipulator(new TabDragManipulator(tabs, Move));
                tabs.Add(tab);
                Add(tab);
            }

            var add = new ToolbarButton { text = "+", tooltip = "Create a new profile" };
            add.clicked += Create;
            Add(add);

            var spacer = new ToolbarSpacer();
            spacer.style.flexGrow = 1f;
            Add(spacer);

            var settings = new ToolbarToggle { text = "Profile Settings", value = settingsVisible };
            settings.SetEnabled(shown != null);
            settings.RegisterValueChangedCallback(evt =>
            {
                settingsVisible = evt.newValue;
                settingsToggled(evt.newValue);
            });
            Add(settings);
        }

        private void Move(int from, int to)
        {
            BuilderPrefs.TabOrder = ProfileOrder.Move(profiles.Select(GuidOf).ToList(), from, to);
            List<SpriteAnimationProfile> sorted = ProfileOrder.Sort(profiles, GuidOf, item => item.DisplayName, BuilderPrefs.TabOrder);
            profiles.Clear();
            profiles.AddRange(sorted);
            schedule.Execute(Rebuild);
        }

        private void Create()
        {
            string path = EditorUtility.SaveFilePanelInProject("New Sprite Animation Profile", "NewProfile", "asset", "Choose where to save the profile.", BuilderPrefs.LastProfileFolder);
            if (string.IsNullOrEmpty(path)) return;

            BuilderPrefs.LastProfileFolder = Path.GetDirectoryName(path).Replace('\\', '/');
            var profile = ScriptableObject.CreateInstance<SpriteAnimationProfile>();
            AssetDatabase.CreateAsset(profile, path);
            created(profile);
        }
    }
}
