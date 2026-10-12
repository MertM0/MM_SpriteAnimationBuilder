using System;
using UnityEngine;

namespace MM.SpriteAnimation
{
    [Serializable]
    internal sealed class TrackingSettings
    {
        [SerializeField] private bool enabled;
        [SerializeField] private string token = string.Empty;
        [SerializeField] private string targetPath = string.Empty;
        [SerializeField] private SpriteAnimationProfile trackedProfile;
        [SerializeField] private bool snapToPixel = true;
        [SerializeField] private float rotationDeadZone = 1f;

        internal bool Enabled => enabled;
        internal string Token => token ?? string.Empty;
        internal string FolderName => Token + "~";
        internal string TargetPath => targetPath ?? string.Empty;
        internal SpriteAnimationProfile TrackedProfile => trackedProfile;
        internal bool SnapToPixel => snapToPixel;
        internal float RotationDeadZone => rotationDeadZone;
    }
}
