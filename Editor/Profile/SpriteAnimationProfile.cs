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
        [SerializeField] private SpriteAnimationProfile variantOf;
        [SerializeField] private string folderPrefix = string.Empty;
        [SerializeField] private List<AnimationFallback> fallbacks = new List<AnimationFallback>();
        [SerializeField] private List<AnimationEntry> animations = new List<AnimationEntry>();

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;

        internal SpriteAnimationProfile RawVariantOf => variantOf;
        internal bool HasMissingBase => !ReferenceEquals(variantOf, null) && variantOf == null;
        internal SpriteAnimationProfile VariantOf => IsUsableBase(variantOf) ? variantOf : null;
        internal bool IsVariant => VariantOf != null;
        internal string FolderPrefix => folderPrefix ?? string.Empty;
        internal IReadOnlyList<AnimationFallback> Fallbacks => Shared.fallbacks;

        internal string SpriteRootPath => FolderPath(spriteRoot);
        internal string ClipRootPath => FolderPath(clipRoot);
        internal RuntimeAnimatorController Controller => controller;
        internal string Layer => Shared.layer ?? string.Empty;
        internal string ClipPrefix => clipPrefix ?? string.Empty;
        internal string FilePrefix => filePrefix ?? string.Empty;
        internal bool CreateMissingStates => Shared.createMissingStates;
        internal int DefaultFrameRate => Shared.defaultFrameRate;
        internal bool MultiPart => Shared.multiPart;
        internal IReadOnlyList<SpritePart> Parts => Shared.parts;
        internal float PixelsPerUnit => Shared.pixelsPerUnit;
        internal bool FlipX => Shared.flipX;
        internal SpritePivotMode Pivot => Shared.pivot;
        internal Vector2 CustomPivot => Shared.customPivot;
        internal Sprite AnchorSprite => Shared.anchorSprite;
        internal SpriteImportSettings Import => Shared.import;
        internal int MinTickRate => Shared.tickGrid ? Mathf.Clamp(Shared.minTickRate, 1, MaxTickRate) : 1;
        internal bool EventSync => Shared.eventSync;
        internal TrackingSettings Tracking => Shared.tracking;
        internal IReadOnlyList<AnimationEntry> Animations => animations;

        private SpriteAnimationProfile Shared => VariantOf ?? this;

        internal string ClipPath(string animation) => $"{ClipRootPath}/{ClipPrefix}{animation}.anim";

        internal AnimationEntry FindEntry(string animation)
        {
            foreach (AnimationEntry entry in animations)
            {
                if (string.Equals(entry.Name, animation, StringComparison.Ordinal)) return entry;
            }
            return null;
        }

        internal int FrameRateOf(string animation)
        {
            AnimationEntry entry = FindEntry(animation);
            if (IsVariant && (entry == null || !entry.OwnFrameRate)) return VariantOf.FrameRateOf(animation);
            return entry?.FrameRate ?? defaultFrameRate;
        }

        internal bool InheritsFrameRate(string animation) => IsVariant && !(FindEntry(animation)?.OwnFrameRate ?? false);
        internal bool LoopOf(string animation) => Shared.FindEntry(animation)?.Loop ?? false;
        internal bool IsBuilt(string animation) => FindEntry(animation)?.IsBuilt ?? false;
        internal ClipTiming KnownTimingOf(string animation) => ClipTiming.TryParse(FindEntry(animation)?.Timing, out ClipTiming known) ? known : null;

        internal void SetVariantOf(SpriteAnimationProfile value)
        {
            variantOf = value;
            EditorUtility.SetDirty(this);
        }

        internal void SetController(RuntimeAnimatorController value)
        {
            controller = value;
            EditorUtility.SetDirty(this);
        }

        internal void SetRowSettings(string animation, int frameRate, bool loop)
        {
            bool own = IsVariant && frameRate != VariantOf.FrameRateOf(animation);
            AnimationEntry entry = FindEntry(animation);
            if (entry != null && entry.FrameRate == frameRate && entry.Loop == loop && entry.OwnFrameRate == own) return;

            Undo.RecordObject(this, "Sprite Animation Row Settings");
            (entry ?? AddEntry(animation)).SetSettings(frameRate, loop, own);
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
            AnimationEntry entry = AnimationEntry.Create(animation, FrameRateOf(animation));
            animations.Add(entry);
            return entry;
        }

        private bool IsUsableBase(SpriteAnimationProfile candidate) => candidate != null && candidate != this && candidate.variantOf == null;

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
