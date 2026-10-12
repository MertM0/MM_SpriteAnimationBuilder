using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace MM.SpriteAnimation
{
    internal static class AnimationFingerprint
    {
        private static readonly MD5 Md5 = MD5.Create();
        private static readonly FileStampCache<ulong> ContentHashes = new FileStampCache<ulong>(path => BitConverter.ToUInt64(Md5.ComputeHash(File.ReadAllBytes(path)), 0));

        internal static ulong ProfileSignature(SpriteAnimationProfile profile)
        {
            StableHash hash = StableHash.Create();
            hash.Add(profile.ClipRootPath);
            hash.Add(profile.ClipPrefix);
            hash.Add(profile.FilePrefix);
            hash.Add(Mathf.RoundToInt(profile.PixelsPerUnit * 1000f));
            hash.Add(profile.FlipX ? 1 : 0);
            hash.Add((int)profile.Pivot);
            if (profile.Pivot == SpritePivotMode.Anchor) AddSprite(ref hash, profile.AnchorSprite);
            foreach (SpritePart part in PartLayout.ActiveParts(profile))
            {
                hash.Add(part.Token);
                hash.Add(part.RendererPath);
                if (part.Optional) hash.Add(1);
            }

            TrackingSettings tracking = profile.Tracking;
            hash.Add(tracking.Enabled ? 1 : 0);
            if (tracking.Enabled)
            {
                hash.Add(tracking.Token);
                hash.Add(tracking.TargetPath);
                hash.Add(tracking.SnapToPixel ? 1 : 0);
                hash.Add(Mathf.RoundToInt(tracking.RotationDeadZone * 1000f));
                AddSprite(ref hash, tracking.TrackedProfile.AnchorSprite);
            }

            AddExtensions(profile, ref hash);
            return hash.Value;
        }

        internal static string Fingerprint(SpriteAnimationProfile profile, AnimationSource source, ulong signature)
        {
            var hash = new StableHash(signature);
            hash.Add(profile.FrameRateOf(source.Name));
            hash.Add(profile.LoopOf(source.Name) ? 1 : 0);
            foreach (PartFrames part in source.Parts)
            {
                hash.Add(part.Part.Token);
                foreach (string frame in part.Frames)
                {
                    if (frame == null) hash.Add(-1L);
                    else hash.AddFileSize(frame);
                }
            }
            if (source.ReferenceFrames != null)
            {
                if (source.ReferenceFrames.Count == 0) hash.Add(0);
                foreach (string frame in source.ReferenceFrames) hash.AddFileContent(frame, ContentHash(frame));
            }

            int frameRate = Mathf.Max(1, profile.FrameRateOf(source.Name));
            int tickRate = ClipTiming.TickRateOf(frameRate, profile.MinTickRate);
            if (tickRate != ClipTiming.TickRateOf(frameRate, ClipTiming.DefaultMinTickRate))
            {
                hash.Add("tick");
                hash.Add(tickRate);
            }
            return hash.Value.ToString("x16", CultureInfo.InvariantCulture);
        }

        private static void AddExtensions(SpriteAnimationProfile profile, ref StableHash hash)
        {
            if (profile.Pivot == SpritePivotMode.Custom)
            {
                hash.Add("pivot.custom");
                hash.Add(Mathf.RoundToInt(profile.CustomPivot.x * 10000f));
                hash.Add(Mathf.RoundToInt(profile.CustomPivot.y * 10000f));
            }

            SpriteImportSettings import = profile.Import;
            if (import.FilterMode != ImportFilterMode.Keep)
            {
                hash.Add("import.filter");
                hash.Add((int)import.FilterMode);
            }
            if (import.Compression != ImportCompression.Keep)
            {
                hash.Add("import.compression");
                hash.Add((int)import.Compression);
            }
            if (import.MeshType != SpriteMeshType.Tight)
            {
                hash.Add("import.mesh");
                hash.Add((int)import.MeshType);
            }
            if (import.MaxTextureSize != ImportMaxSize.Keep)
            {
                hash.Add("import.maxSize");
                hash.Add((int)import.MaxTextureSize);
            }
            if (profile.Layer.Length > 0)
            {
                hash.Add("layer");
                hash.Add(profile.Layer);
            }
            if (profile.Controller is AnimatorOverrideController overrideController)
            {
                hash.Add("override");
                hash.Add(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(overrideController)));
            }
        }

        private static void AddSprite(ref StableHash hash, Sprite sprite)
        {
            string path = sprite != null ? AssetDatabase.GetAssetPath(sprite) : null;
            if (string.IsNullOrEmpty(path)) hash.Add(0L);
            else hash.AddFileContent(path, ContentHash(path));
        }

        private static ulong ContentHash(string path) => File.Exists(path) ? ContentHashes.Get(path) : 0UL;
    }
}
