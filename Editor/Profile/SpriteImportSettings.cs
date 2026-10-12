using System;
using UnityEngine;

namespace MM.SpriteAnimation
{
    [Serializable]
    internal sealed class SpriteImportSettings
    {
        [SerializeField] private ImportFilterMode filterMode;
        [SerializeField] private ImportCompression compression;
        [SerializeField] private SpriteMeshType meshType = SpriteMeshType.Tight;
        [SerializeField] private ImportMaxSize maxTextureSize;

        internal ImportFilterMode FilterMode => filterMode;
        internal ImportCompression Compression => compression;
        internal SpriteMeshType MeshType => meshType;
        internal ImportMaxSize MaxTextureSize => maxTextureSize;
    }
}
