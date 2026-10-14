using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace MM.SpriteAnimation
{
    internal static class ProfileVariants
    {
        internal static void Check(SpriteAnimationProfile profile, SpriteAnimationScan scan)
        {
            if (profile.HasMissingBase)
            {
                scan.AddError("Base profile is missing.");
                return;
            }

            SpriteAnimationProfile raw = profile.RawVariantOf;
            if (raw == null) return;
            if (raw == profile)
            {
                scan.AddError("A profile cannot be a variant of itself.");
                return;
            }
            if (raw.RawVariantOf != null)
            {
                scan.AddError($"'{raw.DisplayName}' is itself a variant; variants are one level deep.");
                return;
            }

            foreach (string clash in ClipClashes(profile, new[] { raw })) scan.AddError(clash);

            RuntimeAnimatorController own = profile.Controller;
            RuntimeAnimatorController shared = raw.Controller;
            if (shared == null) return;
            if (own == null)
            {
                scan.AddWarning("No controller; clips are built but no state is assigned. Use Create Override.");
                return;
            }
            if (own == shared)
            {
                scan.AddError("Uses the base's controller; each variant needs its own (Create Override).");
                return;
            }

            AnimatorController sharedRoot = ControllerBinding.BaseOf(shared);
            if (own is AnimatorOverrideController && sharedRoot != null && ControllerBinding.BaseOf(own) != sharedRoot)
            {
                scan.AddWarning($"The override's base controller is not '{sharedRoot.name}'; state names may differ.");
            }
            else if (own is AnimatorController)
            {
                scan.AddWarning($"Uses a separate Animator Controller; state names may differ from '{raw.DisplayName}'.");
            }
        }

        internal static IEnumerable<string> ClipClashes(SpriteAnimationProfile profile, IEnumerable<SpriteAnimationProfile> others)
        {
            if (profile.ClipRootPath == null) yield break;

            foreach (SpriteAnimationProfile other in others)
            {
                if (other == null || other == profile || other.ClipRootPath != profile.ClipRootPath || other.ClipPrefix != profile.ClipPrefix) continue;
                yield return $"Clip Root and Clip Prefix match '{other.DisplayName}'; clips would overwrite each other.";
            }
        }

        internal static bool CanCreateOverride(SpriteAnimationProfile profile, out string reason)
        {
            reason = null;
            if (!profile.IsVariant || profile.Controller != null) return false;
            if (ControllerBinding.BaseOf(profile.VariantOf.Controller) == null)
            {
                reason = "The base profile has no controller.";
                return false;
            }
            if (profile.ClipRootPath == null)
            {
                reason = "Assign Clip Root first.";
                return false;
            }
            return true;
        }

        internal static AnimatorOverrideController CreateOverride(SpriteAnimationProfile profile, SpriteAnimationReport report)
        {
            if (!CanCreateOverride(profile, out _)) return null;

            RuntimeAnimatorController baseController = profile.VariantOf.Controller;
            AnimatorController root = ControllerBinding.BaseOf(baseController);
            string path = AssetDatabase.GenerateUniqueAssetPath($"{profile.ClipRootPath}/{profile.DisplayName}_Animator.overrideController");
            var created = new AnimatorOverrideController(root) { name = Path.GetFileNameWithoutExtension(path) };
            AssetDatabase.CreateAsset(created, path);
            profile.SetController(created);
            AssetDatabase.SaveAssetIfDirty(profile);
            if (baseController is AnimatorOverrideController)
            {
                report?.Warning(null, $"'{profile.VariantOf.DisplayName}' uses an override; the new override starts from its root '{root.name}'.");
            }
            return created;
        }

        internal static SpriteAnimationProfile CreateVariant(SpriteAnimationProfile baseProfile, string path)
        {
            var created = ScriptableObject.CreateInstance<SpriteAnimationProfile>();
            AssetDatabase.CreateAsset(created, path);
            created.SetVariantOf(baseProfile);
            EditorUtility.SetDirty(created);
            AssetDatabase.SaveAssetIfDirty(created);
            return created;
        }
    }
}
