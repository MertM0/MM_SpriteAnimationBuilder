using System;
using UnityEngine;

namespace MM.SpriteAnimation
{
    [Serializable]
    internal sealed class AnimationEntry
    {
        [SerializeField] private string name;
        [SerializeField] private int frameRate;
        [SerializeField] private bool loop;
        [SerializeField] private string fingerprint;
        [SerializeField] private string timing;
        [SerializeField] private bool ownFrameRate;

        internal string Name => name;
        internal int FrameRate => frameRate;
        internal bool Loop => loop;
        internal string Fingerprint => fingerprint;
        internal string Timing => timing;
        internal bool IsBuilt => !string.IsNullOrEmpty(fingerprint);
        internal bool OwnFrameRate => ownFrameRate;

        internal static AnimationEntry Create(string animation, int frameRate) => new AnimationEntry { name = animation, frameRate = frameRate };

        internal void SetSettings(int newFrameRate, bool newLoop, bool newOwnFrameRate)
        {
            frameRate = newFrameRate;
            loop = newLoop;
            ownFrameRate = newOwnFrameRate;
        }

        internal void SetFingerprint(string value) => fingerprint = value;

        internal void SetTiming(string value) => timing = value;
    }
}
