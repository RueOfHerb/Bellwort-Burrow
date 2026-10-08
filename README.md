# Bellwort Burrow

A cozy crafting and life sim, made in Unity 6 (6000.5.7f1) with URP 2D lighting and 16 px pixel art.

## Getting started

The code, settings and scenes are in git. The art is a separate download, so the repo stays small.

1. Clone the repo.
2. Download the latest art package (`BellwortBurrow-Art-<date>.zip`) from the [Bellwort Burrow Art folder on Google Drive](https://drive.google.com/drive/folders/1vh2IrLCfF2ghyih9uQhOeNi9hRS9URCC). Ask Jocelyn for access if the link doesn't open for you.
3. Open the project in Unity 6000.5.7f1. The console warns about missing art until the next step.
4. Choose **Bellwort Burrow > Art Package > Install Art Package...** and pick the zip. It fills in `Assets/Art`, the art's `.meta` files included, so scenes find every sprite.
5. Open `Assets/Scenes/Zones/ForlornForest.unity` to see the forest hamlet. There's no player yet; Play shows it through the game camera.

You can also unzip the package by hand: extract it into the project folder (the one with `Assets` in it) so that it creates `Assets/Art`.

## What lives where

| | In git | In the art package |
|---|---|---|
| Code, assembly definitions, tests | ✓ | |
| Project settings, render pipeline assets | ✓ | |
| Scenes | ✓ | |
| Aseprite sheets, palettes and their `.meta` files | | ✓ (`Assets/Art`) |
| Prefabs, stacked trees, tile palettes made from the sheets | | ✓ (`Assets/Art/Prefabs`, `Assets/Art/Tile Palettes`) |

Scenes point at the art by the GUIDs in its `.meta` files, which is why the `.meta` files travel with the art. Old art versions stay on the artist's machine in `Art Archive/`, which git ignores.

## Working on art

The art notes are in `Assets/Art/README.md` once the package is installed. In short:

- Edit the sheets in Aseprite and save. Unity reimports them on its own through the Bellwort Sheet Importer (`Assets/Scripts/Editor/Art`), which turns every Aseprite slice into a named sprite, plus a `_glow` sprite for anything on the Glow layer.
- After adding or renaming slices, run **Bellwort Burrow > Setup > Set Up Forlorn Forest (All Steps)**. It applies the pixel art settings, rebuilds the prefabs (with their glow and lights), stacks the tall trees, repaints the Forest Ground tile palette and rebuilds the Forlorn Forest scene. Each step is also under **Setup > Forest Steps**.
- To share art changes, choose **Bellwort Burrow > Art Package > Export Art Package**. It writes a dated zip to `ArtPackage/` next to `Assets`. Upload that to the Drive folder.

## Pixel art setup

- 16 px tiles, 16 pixels per unit, 1 tile is 2 feet
- Pixel Perfect Camera at 480x270, upscaled from a render texture
- Sprites sort by height (custom axis 0, 1, 0) with their pivot at their feet
- Each zone has a 2D global light; the Forlorn Forest's is a tranquil blue (#A4B8E6) at 72%. Glows use an unlit material so they shine through it.

## Design docs

Lore, story and ideas are in `Design/`.
