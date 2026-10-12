using System.Collections.Generic;

namespace MM.SpriteAnimation
{
    internal sealed class PartFrames
    {
        internal PartFrames(SpritePart part, IReadOnlyList<string> frames)
        {
            Part = part;
            Frames = frames;
        }

        internal SpritePart Part { get; }
        internal IReadOnlyList<string> Frames { get; }
    }
}
