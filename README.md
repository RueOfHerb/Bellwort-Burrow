# Bellwort Burrow

A cozy crafting and life sim, made in Unity 6 (6000.5.7f1) with URP 2D lighting and 16 px pixel art.

The code, settings and scenes are in git. The art is a separate download, the art package, so the repo stays small. Art packages live in the `ArtPackage` folder next to `Assets`: git keeps the empty folder but never the zips, so downloads go there, exports land there, and the tools look there on their own. Getting the art and sharing it are their own steps, and each one checks before it changes anything.

## Getting started

1. Clone the repo.
2. Open the project in Unity 6000.5.7f1. Scenes show missing prefabs until the art is installed.
3. Download the art (see below).
4. Open `Assets/Scenes/Zones/ForlornForest.unity` to see the forest hamlet. There's no player yet; Play shows it through the game camera.

## Downloading the art

Do this when you first set up, and whenever someone has shared new art.

1. **Download** the latest `BellwortBurrow-Art-<date>.zip` from the [Bellwort Burrow Art folder on Google Drive](https://drive.google.com/drive/folders/1vh2IrLCfF2ghyih9uQhOeNi9hRS9URCC) into the `ArtPackage` folder in the project. Ask Jocelyn for access if the link doesn't open for you.
2. **Check:** in Unity, choose **Bellwort Burrow > Art Package > Check And Install Art Package...**. It picks up the newest zip in `ArtPackage` by itself (if there isn't one, it offers to open the Drive folder and the `ArtPackage` folder). Before writing anything, it compares the package with the art you already have and tells you how many files it would add or update. It warns you about any file you changed since the last package you installed or exported, since that may be your own unshared work. (Each machine keeps a small record of its last package in `ArtPackage/last-sync.txt`, which git ignores. Without one, every difference counts as yours.)
3. **Confirm:** choose **Install** (or **Install, Keep My Changes** when it warned you). Choose **Install, Replace Everything** only when you're sure you want the package's version of those files. Files you have that aren't in the package are never touched.

The package fills in `Assets/Art`, the art's `.meta` files included, so scenes find every sprite.

## Sharing art changes

Do this only once a round of art is finished.

1. **Finish:** save the sheets in Aseprite, run **Bellwort Burrow > Setup > Set Up Forest Art (All Steps)** if you added or renamed slices, and save your scenes.
2. **Check:** choose **Bellwort Burrow > Art Package > Check And Export Art Package...**. It saves open scenes, checks that the prefabs and tiles are up to date with the sheets, and lists anything you changed by hand.
3. **Confirm:** it asks whether you're done and ready to export. Only then does it write a dated zip to the `ArtPackage` folder.
4. **Upload** the zip from `ArtPackage` to the Drive folder. If Drive asks, choose **Replace existing file** so the download link stays the same.
5. **Commit** your code and scene changes to git as usual.

## What lives where

| | In git | In the art package |
|---|---|---|
| Code, assembly definitions, tests | ✓ | |
| Project settings, render pipeline assets | ✓ | |
| Scenes | ✓ | |
| Aseprite sheets, palettes and their `.meta` files | | ✓ (`Assets/Art`) |
| Prefabs, stacked trees, ground tiles and tile palettes made from the sheets | | ✓ (`Assets/Art/Prefabs`, `Assets/Art/Tiles`, `Assets/Art/Tile Palettes`) |

Scenes point at the art by the GUIDs in its `.meta` files, which is why the `.meta` files travel with the art. The zips themselves sit in `ArtPackage/`, which git keeps empty (just a `.gitkeep`). Old art versions stay on the artist's machine in `Art Archive/`, and one-off scripts that once placed things in a scene stay in `tmp/`; git ignores both.

## Building scenes

Everything in a scene is placed by hand; only backgrounds and skyboxes are exceptions.

- **Objects:** drag prefabs from `Assets/Art/Prefabs` into the scene. Their pivot is at their feet, so they sort correctly as you move them up and down.
- **Ground:** open **Window > 2D > Tile Palette**, pick the **Forest Ground** palette, set **Active Tilemap** to the layer you want, and paint. The top row of the palette holds the tiles you paint with:
  - **Forest Grass** on the **Grass** layer picks a random grass variation for each tile.
  - **Forest Deer Trail** on the **Deer Trails** layer and **Forest Path** on the **Paths** layer are Unity Auto Tiles (2x2 mask). Paint where the trail or path goes and Unity picks the edge pieces. Paint them at least two tiles wide: the edges land halfway into the outer tiles, so three painted rows make a path two tiles wide.
  - Tufts and crop circles go on the **Tufts and Crop Circles** layer. Drag a box around a whole crop circle in the palette to stamp it in one click.
- Save the scene, and it's in git.

No tool in the project rebuilds a scene. The one-off script that first laid out the Forlorn Forest is kept in `tmp/`, and it checks the scene's fingerprint before doing anything: if the scene changed since that script built it, it refuses (the Forlorn Forest has changed, so it will never be rebuilt). The setup tools also never write over a prefab, tile or palette you changed by hand: each thing they make is stamped with a fingerprint, and if it changed since, they leave it alone and list it in the console. To let setup rebuild something you changed (throwing your changes away), select it or its folder and choose **Assets > Bellwort Burrow > Let Setup Rebuild Selected**.

For future tilesets, use Unity's own tiles from 2D Tilemap Extras: a 2x2 Auto Tile (16 pieces) for organic areas painted two or more tiles wide, like paths, water or meadows, and a 3x3 Auto Tile (47 pieces) for anything that has to work one tile at a time, like farmland or fences.

## Working on art

The art notes are in `Assets/Art/README.md` once the package is installed. In short:

- Edit the sheets in Aseprite and save. Unity reimports them on its own through the Bellwort Sheet Importer (`Assets/Scripts/Editor/Art`), which turns every Aseprite slice into a named sprite, plus a `_glow` sprite for anything on the Glow layer.
- After adding or renaming slices, run **Bellwort Burrow > Setup > Set Up Forest Art (All Steps)**. It applies the pixel art settings, rebuilds the prefabs (with their glow and lights), stacks the tall trees, refreshes the ground tiles and repaints the Forest Ground tile palette, skipping anything changed by hand. Each step is also under **Setup > Forest Steps**.
- When the round is finished, share it (see Sharing art changes).

## Pixel art setup

- 16 px tiles, 16 pixels per unit, 1 tile is 2 feet
- Pixel Perfect Camera at 480x270, upscaled from a render texture
- Sprites sort by height (custom axis 0, 1, 0) with their pivot at their feet
- Each zone has a 2D global light; the Forlorn Forest's is a tranquil blue (#A4B8E6) at 72%. Glows use an unlit material so they shine through it.
- 2D lights only light the sorting layers listed on them. If you add a sorting layer, lights in open scenes pick it up, but run **Set Up Forest Art** again so the lights inside the prefabs do too, and check the zone light in any scene that wasn't open.

## Tests

Open **Window > General > Test Runner**, pick **EditMode** and choose **Run All**. `Assets/Tests/EditMode` covers the game systems and `Assets/Tests/Editor` covers the art tools: reading Aseprite files, the tile corner numbering, the fingerprints that protect hand edits, and the art package's install rules.

## Design docs

Lore, story and ideas are in `Design/`.
