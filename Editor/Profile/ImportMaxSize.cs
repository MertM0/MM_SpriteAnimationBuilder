using UnityEngine;

namespace MM.SpriteAnimation
{
    internal enum ImportMaxSize
    {
        Keep = 0,
        [InspectorName("256")] Size256 = 256,
        [InspectorName("512")] Size512 = 512,
        [InspectorName("1024")] Size1024 = 1024,
        [InspectorName("2048")] Size2048 = 2048,
        [InspectorName("4096")] Size4096 = 4096,
        [InspectorName("8192")] Size8192 = 8192,
    }
}
