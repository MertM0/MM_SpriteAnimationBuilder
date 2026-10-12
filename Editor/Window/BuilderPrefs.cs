using System;
using System.Collections.Generic;
using UnityEditor;

namespace MM.SpriteAnimation
{
    internal static class BuilderPrefs
    {
        private const string Prefix = "MM.SpriteAnimationBuilder.";
        private const string SelectedProfileName = "SelectedProfile";
        private const string TabOrderName = "TabOrder";
        private const string LastProfileFolderName = "LastProfileFolder";
        private const string DefaultFolder = "Assets";
        private const char OrderSeparator = ';';

        internal static string SelectedProfile
        {
            get => EditorPrefs.GetString(Key(SelectedProfileName), string.Empty);
            set => EditorPrefs.SetString(Key(SelectedProfileName), value ?? string.Empty);
        }

        internal static IReadOnlyList<string> TabOrder
        {
            get => EditorPrefs.GetString(Key(TabOrderName), string.Empty).Split(new[] { OrderSeparator }, StringSplitOptions.RemoveEmptyEntries);
            set => EditorPrefs.SetString(Key(TabOrderName), value == null ? string.Empty : string.Join(OrderSeparator.ToString(), value));
        }

        internal static string LastProfileFolder
        {
            get
            {
                string stored = EditorPrefs.GetString(Key(LastProfileFolderName), DefaultFolder);
                return AssetDatabase.IsValidFolder(stored) ? stored : DefaultFolder;
            }
            set => EditorPrefs.SetString(Key(LastProfileFolderName), value ?? DefaultFolder);
        }

        internal static string Key(string name) => $"{Prefix}{PlayerSettings.productGUID:N}.{name}";
    }
}
