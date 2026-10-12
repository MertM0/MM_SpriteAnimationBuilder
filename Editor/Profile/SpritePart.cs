using System;
using UnityEngine;

namespace MM.SpriteAnimation
{
    [Serializable]
    internal sealed class SpritePart
    {
        [SerializeField] private string token = string.Empty;
        [SerializeField] private string rendererPath = string.Empty;
        [SerializeField] private bool optional;

        internal string Token => token ?? string.Empty;
        internal string RendererPath => rendererPath ?? string.Empty;
        internal bool Optional => optional;
        internal string Label => Token.Length == 0 ? "Frames" : Token;

        internal static SpritePart Single(string rendererPath) => new SpritePart { rendererPath = rendererPath };
    }
}
