using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MM.SpriteAnimation
{
    internal static class SpriteImport
    {
        internal static int Apply(SpriteAnimationProfile profile, IReadOnlyList<AnimationPlan> plans, ProfileContext context)
        {
            (SpriteAlignment alignment, Vector2 pivot) = SpritePivot.Resolve(profile, context.Anchor);

            IEnumerable<string> paths = plans.SelectMany(plan => plan.Source.Parts).SelectMany(part => part.Frames).Where(path => path != null);
            string anchorPath = SpritePivot.ImportPath(profile);
            if (anchorPath != null) paths = paths.Append(anchorPath);

            int reimported = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string path in paths.Distinct(StringComparer.Ordinal))
                {
                    if (ApplyOne(path, profile, alignment, pivot)) reimported++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            return reimported;
        }

        private static bool ApplyOne(string path, SpriteAnimationProfile profile, SpriteAlignment alignment, Vector2 pivot)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);

            SpriteImportSettings import = profile.Import;
            bool setFilter = import.FilterMode != ImportFilterMode.Keep;
            bool setCompression = import.Compression != ImportCompression.Keep;
            bool setMaxSize = import.MaxTextureSize != ImportMaxSize.Keep;

            bool upToDate = settings.textureType == TextureImporterType.Sprite
                && settings.spriteMode == (int)SpriteImportMode.Single
                && Mathf.Approximately(settings.spritePixelsPerUnit, profile.PixelsPerUnit)
                && !settings.mipmapEnabled
                && settings.spriteMeshType == import.MeshType
                && settings.spriteAlignment == (int)alignment
                && (alignment != SpriteAlignment.Custom || (settings.spritePivot - pivot).sqrMagnitude < 1e-10f)
                && (!setFilter || settings.filterMode == ToUnity(import.FilterMode))
                && (!setCompression || importer.textureCompression == ToUnity(import.Compression))
                && (!setMaxSize || importer.maxTextureSize == (int)import.MaxTextureSize);
            if (upToDate) return false;

            settings.textureType = TextureImporterType.Sprite;
            settings.spriteMode = (int)SpriteImportMode.Single;
            settings.spritePixelsPerUnit = profile.PixelsPerUnit;
            settings.mipmapEnabled = false;
            settings.spriteMeshType = import.MeshType;
            settings.spriteAlignment = (int)alignment;
            settings.spritePivot = pivot;
            if (setFilter) settings.filterMode = ToUnity(import.FilterMode);
            importer.SetTextureSettings(settings);

            if (setCompression) importer.textureCompression = ToUnity(import.Compression);
            if (setMaxSize) importer.maxTextureSize = (int)import.MaxTextureSize;
            importer.SaveAndReimport();
            return true;
        }

        private static TextureImporterCompression ToUnity(ImportCompression value)
        {
            switch (value)
            {
                case ImportCompression.Low: return TextureImporterCompression.CompressedLQ;
                case ImportCompression.Normal: return TextureImporterCompression.Compressed;
                case ImportCompression.High: return TextureImporterCompression.CompressedHQ;
                default: return TextureImporterCompression.Uncompressed;
            }
        }

        private static FilterMode ToUnity(ImportFilterMode value)
        {
            switch (value)
            {
                case ImportFilterMode.Bilinear: return FilterMode.Bilinear;
                case ImportFilterMode.Trilinear: return FilterMode.Trilinear;
                default: return FilterMode.Point;
            }
        }
    }
}
