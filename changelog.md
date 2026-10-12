# Changelog

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
