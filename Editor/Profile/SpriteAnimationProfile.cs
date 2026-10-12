using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MM.SpriteAnimation
{
    [CreateAssetMenu(menuName = "MM/Sprite Animation Profile", fileName = "SpriteAnimationProfile")]
    public sealed class SpriteAnimationProfile : ScriptableObject
    {
        private const int MaxTickRate = 240;

        [SerializeField] private string displayName;
        [SerializeField] private DefaultAsset spriteRoot;
        [SerializeField] private DefaultAsset clipRoot;
        [SerializeField] private RuntimeAnimatorController controller;
        [SerializeField] private string layer = string.Empty;
        [SerializeField] private string clipPrefix;
        [SerializeField] private string filePrefix;
        [SerializeField] private bool createMissingStates;
        [SerializeField] private int defaultFrameRate = 12;
        [SerializeField] private bool multiPart;
        [SerializeField] private List<SpritePart> parts = new List<SpritePart> { new SpritePart() };
        [SerializeField] private float pixelsPerUnit = 100f;
        [SerializeField] private bool flipX;
        [SerializeField] private SpritePivotMode pivot = SpritePivotMode.Center;
        [SerializeField] private Vector2 customPivot = new Vector2(0.5f, 0.5f);
        [SerializeField] private Sprite anchorSprite;
        [SerializeField] private SpriteImportSettings import = new SpriteImportSettings();
        [SerializeField] private bool tickGrid = true;
        [SerializeField] private int minTickRate = ClipTiming.DefaultMinTickRate;
        [SerializeField] private bool eventSync = true;
        [SerializeField] private TrackingSettings tracking = new TrackingSettings();
        [SerializeField] private List<AnimationEntry> animations = new List<AnimationEntry>();

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;

        internal string SpriteRootPath => FolderPath(spriteRoot);
        internal string ClipRootPath => FolderPath(clipRoot);
        internal RuntimeAnimatorController Controller => controller;
        internal string Layer => layer ?? string.Empty;
        internal string ClipPrefix => clipPrefix ?? string.Empty;
        internal string FilePrefix => filePrefix ?? string.Empty;
        internal bool CreateMissingStates => createMissingStates;
        internal int DefaultFrameRate => defaultFrameRate;
        internal bool MultiPart => multiPart;
        internal IReadOnlyList<SpritePart> Parts => parts;
        internal float PixelsPerUnit => pixelsPerUnit;
        internal bool FlipX => flipX;
        internal SpritePivotMode Pivot => pivot;
        internal Vector2 CustomPivot => customPivot;
        internal Sprite AnchorSprite => anchorSprite;
        internal SpriteImportSettings Import => import;
        internal int MinTickRate => tickGrid ? Mathf.Clamp(minTickRate, 1, MaxTickRate) : 1;
        internal bool EventSync => eventSync;
        internal TrackingSettings Tracking => tracking;
        internal IReadOnlyList<AnimationEntry> Animations => animations;

        internal string ClipPath(string animation) => $"{ClipRootPath}/{ClipPrefix}{animation}.anim";

        internal AnimationEntry FindEntry(string animation)
        {
            foreach (AnimationEntry entry in animations)
            {
                if (string.Equals(entry.Name, animation, StringComparison.Ordinal)) return entry;
            }
            return null;
        }

        internal int FrameRateOf(string animation) => FindEntry(animation)?.FrameRate ?? defaultFrameRate;
        internal bool LoopOf(string animation) => FindEntry(animation)?.Loop ?? false;
        internal bool IsBuilt(string animation) => FindEntry(animation)?.IsBuilt ?? false;
        internal ClipTiming KnownTimingOf(string animation) => ClipTiming.TryParse(FindEntry(animation)?.Timing, out ClipTiming known) ? known : null;

        internal void SetRowSettings(string animation, int frameRate, bool loop)
        {
            AnimationEntry entry = FindEntry(animation);
            if (entry != null && entry.FrameRate == frameRate && entry.Loop == loop) return;

            Undo.RecordObject(this, "Sprite Animation Row Settings");
            (entry ?? AddEntry(animation)).SetSettings(frameRate, loop);
            EditorUtility.SetDirty(this);
        }

        internal void SetFingerprint(string animation, string value)
        {
            AnimationEntry entry = FindEntry(animation);
            if (entry != null && entry.Fingerprint == value) return;

            (entry ?? AddEntry(animation)).SetFingerprint(value);
            EditorUtility.SetDirty(this);
        }

        internal void SetKnownTiming(string animation, ClipTiming value)
        {
            string signature = value?.Signature;
            AnimationEntry entry = FindEntry(animation);
            if (entry != null && entry.Timing == signature) return;

            (entry ?? AddEntry(animation)).SetTiming(signature);
            EditorUtility.SetDirty(this);
        }

        private AnimationEntry AddEntry(string animation)
        {
            AnimationEntry entry = AnimationEntry.Create(animation, defaultFrameRate);
            animations.Add(entry);
            return entry;
        }

        private void OnValidate()
        {
            if (parts.Count == 0) parts.Add(new SpritePart());
            minTickRate = Mathf.Clamp(minTickRate, 1, MaxTickRate);
            customPivot = new Vector2(Mathf.Clamp01(customPivot.x), Mathf.Clamp01(customPivot.y));
        }

        private static string FolderPath(DefaultAsset folder)
        {
            if (folder == null) return null;
            string path = AssetDatabase.GetAssetPath(folder);
            return AssetDatabase.IsValidFolder(path) ? path : null;
        }
    }
}
