using System.Collections.Generic;
using UnityEngine;

namespace MM.SpriteAnimation
{
    internal sealed class AnimationPlan
    {
        internal AnimationSource Source;
        internal int FrameRate;
        internal bool Loop;
        internal List<ReferencePose> Poses;
        internal List<Sprite[]> Sprites;
        internal bool ResetTiming;
    }
}
