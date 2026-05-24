# UI Directory Standard

`Assets/_NtingCampus/UI` is the single owning root for this project's UI system.

## Directory Layout

- `Assets/_NtingCampus/UI/Scripts/Editor`
  Editor-only windows and authoring UI tools.
- `Assets/_NtingCampus/UI/Scripts/MapEditor/Runtime`
  Runtime map editor presentation and overlay UI.
- `Assets/_NtingCampus/UI/Scripts/Runtime`
  Runtime gameplay UI entry points.
- `Assets/_NtingCampus/UI/Scripts/Runtime/Canteen`
  Runtime canteen order panel and its presentation-only scripts.
- `Assets/_NtingCampus/UI/Scripts/Runtime/Gameplay`
  HUD, overlays, startup flow, theme, localization helpers, and shared panel tween utilities.
- `Assets/_NtingCampus/UI/Scripts/Runtime/Interaction`
  Interaction prompt model, source, and view.
- `Assets/_NtingCampus/UI/Scripts/Storage`
  Storage window presentation, drag UI, view models, and UI-facing storage helpers.
- `Assets/_NtingCampus/UI/Scripts/WorldSpace`
  World-space UI presentation such as held item visuals and NPC speech bubbles.
- `Assets/_NtingCampus/UI/Storage`
  Storage UI assets, generated art, prefabs, and mod-facing resources.

## Ownership Rules

- New UI scripts must be added under `Assets/_NtingCampus/UI/Scripts`.
- New UI assets and prefabs must be added under `Assets/_NtingCampus/UI`.
- Do not add new UI owners under `Assets/NtingCampus/Runtime`.
- Do not add new UI owners under `Assets/_Nting/UI`.
- Do not recreate `Assets/_NtingCampus/Scripts/UI`.
- If a feature has both gameplay logic and UI, keep the gameplay rule/service in the gameplay subsystem and place only the panel/view/presenter in `Assets/_NtingCampus/UI`.

## Modding Rules

- Mod-facing storage assets belong in `Assets/_NtingCampus/UI/Storage/Resources`.
- Storage prefabs belong in `Assets/_NtingCampus/UI/Storage/Prefabs`.
- Generated storage art belongs in `Assets/_NtingCampus/UI/Storage/Art/Generated`.
- New UI-facing localized text should follow the existing UI text catalogs and localized data fields already used under `Assets/_NtingCampus/UI/Scripts/Runtime/Gameplay` and `Assets/_NtingCampus/UI/Scripts/Storage`.
