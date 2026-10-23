# Bellwort Burrow

A cozy crafting and life sim, made in Unity 6 (6000.5.7f1) with URP 2D lighting and 16 px pixel art.

The code, settings and scenes are in git. The art is a separate download, the art package, so the repo stays small. Art packages live in the `ArtPackage` folder next to `Assets`: git keeps the empty folder but never the zips, so downloads go there, exports land there, and the tools look there on their own. Getting the art and sharing it are their own steps, and each one checks before it changes anything.

## How we work

- Every feature has a page in the **Features** database in Notion (Bellwort Burrow, Compendium > [Features](https://app.notion.com/p/f36db380b84640c88bb9905c8c177064)), keyed by type and number, like `arc-1`. Read a feature's page before changing it, and update the page after.

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
- **Roofs:** a Burrow home's pointed roof is a prefab of its own (`burrow_home_roof_red` and the like, in `Assets/Art/Prefabs/burrow_buildings`). Drop it near a home of its size and it puts itself on the home's sod roof and moves with it; delete it and the home is as it was.
- **Backdrops:** the paintings in `Assets/Art/Prefabs` made from `Assets/Art/Backdrops` (the thicket wall, its darkness, the fallen tree vista and its undergrowth) draw behind everything that stands in the world. The thicket has gaps between its trunks; put **thicket_darkness** at the same spot behind it for a solid dark wall (the Thicket Path), or leave it out to see through the trees (the Bellwort Approach). Add **Parallax** to one to make it drift slower than the ground as the camera moves: Follow 0 stays put like the ground, 1 moves with the camera like the sky (the Bellwort Approach's vista uses 0.4 across). Where you place it is where it sits while the camera is centred on that spot. To put one backdrop behind another, lower its Sorting Group order (the Thicket Path's darkness is at -6, behind the thicket at -5). A backdrop can also go behind the ground tiles (order -32 and -31 in the Bellwort Approach): then it shows only where no ground tile is painted, which is how the approach keeps its vista inside a window north of the band.
- **Camera areas:** a zone that isn't one rectangle, like the Bellwort Approach, gets **Camera Area** objects. Each has **Standing**, where the player stands for it to apply, and **View**, what the camera may show then (yellow and blue boxes in the Scene view); stepping into another area pans the camera across to it. A scene without any keeps the camera on its ground tiles.
- **Ground:** open **Window > 2D > Tile Palette**, pick the zone's palette (**Forest Ground** for the Forlorn Forest, **Burrow Ground** for Bellwort Burrow, with the same tiles named Burrow instead of Forest), choose the layer you want in the **Active Target** dropdown (the layers icon at the top of the Tile Palette window; clicking the layer under Ground in the Hierarchy does the same), and paint. The top row of the palette holds the tiles you paint with:
  - **Forest Grass** on the **Grass** layer picks a random grass variation for each tile.
  - **Forest Deer Trail** on the **Deer Trails** layer and **Forest Path** on the **Paths** layer are Rule Tiles. Paint where the trail or path goes and Unity picks the edge pieces, one tile wide or wider, and a painted staircase reads as a diagonal.
  - **Forest Path Half**, also on the **Paths** layer, is half a tile of path that turns toward the full path beside it, so a path can end half way into a tile or be half a tile wider. **Forest Deer Trail Half**, on the **Deer Trails** layer, does the same for a deer trail.
  - To paint pieces by hand, use the shapes under the top row: Forest Path on the left and the deer trail beside it, each laid out as ready-made shapes (a wide path, one tile wide paths, a wide path narrowing, diagonals, inner corners, bends into slopes). Under them wait the half tiles' shapes for each: a path or trail ending half a tile in or half a tile wider, rounded turns, dots and joins, and inside corners. Drag a box around a shape, or any part of one, to stamp it. Grass, tufts and crop circles sit to the right of the shapes, and at the far right the other looks of each piece wait in runs, a column for the path and one for the deer trail.
  - **Burrow Flagstones** and **Burrow Flagstones Half** (Burrow Ground only) lay flagstones the way Path and Path Half lay a path: paint the patch, any shape down to half a tile, and its edge picks up the grass fringe on its own. Put them on a Tilemap of their own under Paths.
  - **Burrow Water** and **Burrow Water Half** (Burrow Ground only) lay water the same way, a creek or a river as wide as you paint it, on a Water Tilemap of its own. Paint it at least two tiles across so the half tiles can round it off (a one tile creek can only step a whole tile at a time and reads as blocks), paint diagonals as a staircase whose steps share a side for a 45 degree cut, and step the edge with half tiles where it bends gently.
  - **Burrow Waterfall** (Burrow Ground only) is where a creek runs off raised ground: paint the creek's Water straight on through the bank's face, then paint the Waterfall over the face cells it crosses, on a **Waterfalls** Tilemap of its own just above Banks (Order in Layer -26). It goes on a flat run of bank, three rows tall like the face, as wide as the creek; level a 45 degree run for a few columns where a creek goes over (Jocelyn, Oct 22).
  - **Burrow Bank Face** and **Burrow Cliff Face** (Burrow Ground only) make raised ground, on a **Banks** layer of its own (a Tilemap under Ground with Order in Layer -27, between Grass and Deer Trails, so paths, trails and tufts can run across the top). The raised ground itself is plain Burrow Grass. Paint Bank Face (earth) or Cliff Face (stone) in the rows under its front edge, three rows tall, and four rows in each column of a 45 degree run, one row lower per column; the face draws the lip. Homes stand on open ground, not in a bank (Jocelyn, Oct 22).
  - Tufts and crop circles go on the **Tufts and Crop Circles** layer. Drag a box around a whole crop circle in the palette to stamp it in one click.
- **The tree's shadow (Bellwort Burrow only):** the town under the Bellwort Tree gets a **Tree Shadow Clock** (one per zone, next to its Zone Light; give it the zone light and the Grass tilemap). It tells the time: the player's day runs from 7:00 PM, when they wake, to 9:00 AM, when they pass out. The moon lights the town from 7 PM to 5 AM while the trunk's shadow crosses from the lower right to the upper left, then the blue hour has no shadow, and from 7 AM the sunrise brings it back in the lower right at the same pace, so it's only a fifth of the way across by 9 AM. Give it the stump too (Stump, the placed `burrow_stump`) and the shadow is the log's, starting at the top of the stump on its southwest side: it points along the log at midnight, swings from clockwise of it at moonrise to counterclockwise at moonset, and is longest when the light is low and shortest at midnight (the shadow curve's settings, with Log Bearing and Length At Forty Five under From the stump), never narrower than the stump. Its colours, hours and widths are on the component. Tick **Preview** and drag the hour to see any time in the Scene view; untick it and the zone light goes back to how it was.
- Save the scene, and it's in git.

No tool in the project rebuilds a scene. The one-off script that first laid out the Forlorn Forest is kept in `tmp/`, and it checks the scene's fingerprint before doing anything: if the scene changed since that script built it, it refuses (the Forlorn Forest has changed, so it will never be rebuilt). The setup tools also never write over a prefab, tile or palette you changed by hand: each thing they make is stamped with a fingerprint, and if it changed since, they leave it alone and list it in the console. To let setup rebuild something you changed (throwing your changes away), select it or its folder and choose **Assets > Bellwort Burrow > Let Setup Rebuild Selected**.

For future tilesets, use Unity's own tiles from 2D Tilemap Extras: every ground set is a 3x3 Rule Tile with 83 layouts (the 47 of a 3x3 Auto Tile plus 36 slope pieces), painted on the sheet from `GroundAutoTiles.Template`, for paths and trails as much as farmland or fences.

## Working on art

The art notes are in `Assets/Art/README.md` once the package is installed. In short:

- Edit the sheets in Aseprite and save. Unity reimports them on its own through the Bellwort Sheet Importer (`Assets/Scripts/Editor/Art`), which turns every Aseprite slice into a named sprite, plus a `_glow` sprite for anything on the Glow layer.
- After adding or renaming slices, run **Bellwort Burrow > Setup > Set Up Forest Art (All Steps)**. It applies the pixel art settings, rebuilds the prefabs (with their glow and lights), stacks the tall trees, refreshes the ground tiles and repaints the Forest Ground and Burrow Ground tile palettes, skipping anything changed by hand. Each step is also under **Setup > Forest Steps**.
- When the round is finished, share it (see Sharing art changes).

## Pixel art setup

- 16 px tiles, 16 pixels per unit, 1 tile is 2 feet
- Pixel Perfect Camera at 480x270, upscaled from a render texture
- Sprites sort by height (custom axis 0, 1, 0) with their pivot at their feet
- Each zone has a 2D global light; the Forlorn Forest's is a tranquil blue (#A4B8E6) at 72%. Glows use an unlit material so they shine through it.
- Lights render at full size (Light Render Texture Scale 1 on `Assets/Settings/Renderer2D.asset`), one light pixel to an art pixel, so a light's edge, like the tree's shadow, stays crisp instead of blurring over a few pixels.
- 2D lights only light the sorting layers listed on them. If you add a sorting layer, lights in open scenes pick it up, but run **Set Up Forest Art** again so the lights inside the prefabs do too, and check the zone light in any scene that wasn't open.

## Tests

Open **Window > General > Test Runner**, pick **EditMode** and choose **Run All**. `Assets/Tests/EditMode` covers the game systems and `Assets/Tests/Editor` covers the art tools: reading Aseprite files, the tile corner numbering, the fingerprints that protect hand edits, and the art package's install rules.

## Design docs

Lore, story and ideas are in `Design/`.
