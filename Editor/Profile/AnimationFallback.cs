using System;
using UnityEngine;

namespace MM.SpriteAnimation
{
    [Serializable]
    internal sealed class AnimationFallback
    {
        [SerializeField] private string animation;
        [SerializeField] private string source;

        internal string Animation => animation ?? string.Empty;
        internal string Source => source ?? string.Empty;
    }
}
