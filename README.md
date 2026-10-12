# MM Sprite Animation Builder
[![license](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE.md)
[![Unity](https://img.shields.io/badge/Unity-6000.0%2B-blue.svg)](https://unity.com/)
[![version](https://img.shields.io/badge/version-0.1.0-blue.svg)](changelog.md)

_Sprite animation clips and Animator states from numbered PNG folders_

Point a profile at a folder of frame PNGs and an Animator Controller. The builder imports the
sprites, writes one clip per animation folder, assigns it to the matching state and tells you
which animations are new, changed or up to date.

## Installation

Package Manager → **Add package from git URL**:

```
https://github.com/MertM0/MM_SpriteAnimationBuilder.git
```

Or add it to `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.mm.spriteanimationbuilder": "https://github.com/MertM0/MM_SpriteAnimationBuilder.git"
  }
}
```

Requires Unity 6000.0 or newer. Editor only, no dependencies.

## Quick start

1. Open **Tools → MM → Sprite Animation Builder** and press **+** to create a profile.
2. In **Profile Settings** set the **Sprite Root**, the **Clip Root** and, if you want states, a
   **Controller**.
3. Put one folder per animation under the Sprite Root:

   ```
   Sprites/
     Idle/  Idle_01.png  Idle_02.png  ...
     Walk/  Walk_01.png  Walk_02.png  ...
   ```

4. Press **Build All**. Each folder becomes `<Clip Root>/<Clip Prefix><Folder>.anim` and is assigned
   to the state with the same name.

Frames are numbered from `01` without gaps. Each row has its own frame rate and loop setting in the
table; the row menu (**...**) opens the clip, validates or resets the timing.

## Profile options

| Option | What it does |
|---|---|
| **Controller** / **Layer** | Optional. An Animator Controller or an Animator Override Controller; states are read from the chosen layer. Without a controller only clips are built |
| **Create Missing States** | Adds a state for a folder that has none (Animator Controller only) |
| **Multi Part** | Several sprite renderers per animation: `Walk/Body/Walk_Body_01.png`, `Walk/Arm/Walk_Arm_01.png`. A part can be optional (missing frames stay empty) |
| **Pivot** | Center, Bottom, Custom, or Anchor (the midpoint of two marks in an anchor sprite) |
| **Import** | Filter mode, compression, mesh type and max size; **Keep** leaves the importer as it is |
| **Tick Grid** | Builds clips on a fine grid (at least 60 ticks per second) so frame lengths can be adjusted in the Animation window. Off: one key per frame |
| **Event Sync** | Keeps animation events on their frames when you drag keys in the Animation window |
| **Reference Tracking** | Moves a child transform (for example eyes) to follow two marks drawn in an extra `Token~` frame folder |

## Timing and events

With the tick grid on, drag keys in the Animation window's summary row to make single frames
longer or shorter. Rebuilding keeps that timing, changing the frame rate scales it, and events
move with their frames. If frames are added or the Samples field is changed by hand, the row asks
for **Reset Timing and Build** instead of guessing.

## Scripting

```csharp
using MM.SpriteAnimation;

SpriteAnimationReport report = SpriteAnimationBuilder.Build(profile);
if (report.HasErrors) Debug.LogWarning(string.Join("\n", report.Entries));
```

`Build(profile)` builds the same set as **Build All**; pass animation names to build only those.
`Validate` checks without writing anything.

## License

MIT, see [LICENSE.md](LICENSE.md).
