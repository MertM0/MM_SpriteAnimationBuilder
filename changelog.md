# Changelog

## [0.2.0] - 2026-10-14

- Variant profiles: **Variant Of** shares a base profile's settings; variants keep their own roots,
  controller, prefixes and frame rates. **Create Override** builds a variant's override controller.
- Families show as one tab with a member strip, an **Overview** grid and **Build Family**.
- **Fallbacks** build a missing animation from another animation's frames.
- **Folder Prefix** reads exported folders such as `Hero_Walk/` as `Walk`.
- Rows whose state still plays the base controller's clip, or no longer plays the built clip, are
  flagged.

## [0.1.1] - 2026-10-12

- The window menu item moved from `Tools/MM/Sprite Animation Builder` to `Tools/Sprite Animation Builder`.

## [0.1.0] - 2026-10-12

First release.

- Sprite Animation Builder window (`Tools/MM/Sprite Animation Builder`): one tab per profile, a row
  per animation with its status, Build / Validate / Build All, a frame preview and a log.
- Profiles (`Create/MM/Sprite Animation Profile`) turn optional features on per profile: multi part
  sprites, pivot modes (Center, Bottom, Custom, Anchor), reference tracking, the tick grid, event
  sync and import settings.
- Clips are built on a fine tick grid. Frame timing dragged in the Animation window survives rebuilds,
  and animation events stay on their frames when frame lengths change.
- Works without a controller, with an Animator Controller (any layer) or with an Animator Override
  Controller.
- `SpriteAnimationBuilder.Build(profile)` builds from scripts.
