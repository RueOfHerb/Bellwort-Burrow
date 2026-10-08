# Working on Bellwort Burrow with Claude

Indie project, worked on almost always from Jocelyn's main PC, in person or by remote access, and only now and then from another PC. Keep usage low: do the cheap thing by default and save the expensive checks for when they can catch something.

## Session rhythm

- **Start (once per session):** on the main PC, skip the pull unless the last session ended somewhere else. On any other PC, `git pull`. If new art was shared, download the newest zip from Drive into `ArtPackage/` and run Bellwort Burrow > Art Package > Check And Install. Read the Features pages (below) for whatever we're about to touch.
- **During:** commit locally after each finished step. No pushes, pull request edits, CI checks or Drive uploads mid-session.
- **End (when Jocelyn says the session is done):** run the checks below that match what changed, then push once and update the pull request once. If art changed, ask whether the round is done before Check And Export and the Drive upload. Before working on another PC, export from the main PC first so that PC can install the current art. Create or update the Features page for each feature we worked on.
- In a cloud session linked to the PC, the PC's shell has no GitHub login: bundle the session's commits (`git bundle create ArtPackage/x.bundle origin/<branch>..<branch>`), push from the cloud copy, then `git update-ref` the PC's remote branch and delete the bundle.

## Checks, matched to what changed

| What changed | Check |
|---|---|
| Pixels inside existing slices | Nothing. Unity reimports on save. |
| Added or renamed slices | Set Up Forest Art (All Steps), once |
| Editor tool code | Console compiles clean; `Assets/Tests/Editor` tests at session end |
| Game code | `Assets/Tests/EditMode` tests at session end |
| Scenes | Nothing. Jocelyn builds them by hand. |
| Before merging a pull request | All EditMode tests, one setup run, Play mode with a clean Console |

Don't repeat one-time checks (rebuild determinism, fresh-clone GUID scans) unless the prefab tools or the package format change.

## Feature pages

Every feature gets a page in the **Features** database in Notion (Bellwort Burrow, Compendium > Features: https://app.notion.com/p/f36db380b84640c88bb9905c8c177064). It's the memory between sessions and PCs, so keep it current.

- **Key:** a type prefix plus that type's next number, at the start of the title, like `arc-1 Aseprite sheet importer and pixel art settings`. Types: `arc` architecture and tools, `art` art sets, `lvl` levels and scenes, `sys` gameplay systems, `ui` interface, `fix` bug fixes. Search the database for the prefix to find the next number.
- **Sections:** What we built (with file paths), How it works (decisions and why), How to use it, Rules to keep, Tests and checks, Related work (pull request, commits, Drive, Notion pages), Open items.
- **Properties:** Type, Status (Building, In PR, Merged, Replaced), Summary, Systems (Systems & Mechanics entries), Lore (Codex entries), Builds On (earlier features), PR, Branch, Built.
- Mention the key in commit messages for that feature. When a pull request merges, set its features to Merged.

## Looking at Unity cheaply

- Edit files directly on the PC through its shell instead of copying them through the cloud.
- Read Unity's log instead of screenshotting the Console: on Jocelyn's main PC it's `C:\Users\rueof\AppData\Local\Unity\Editor\Editor.log` (ask for read access to that folder once per session; the PC shell sees it under `$HOME/mnt/Editor`).
- Prefer files and logs over screenshots. Take screenshots at half scale and zoom only into the part you need.
- If the Unity MCP server is connected to the session, use it for console logs and menu items instead of driving the editor.

## Standing rules

- Everything in scenes is placed by hand (only backgrounds and skyboxes are exceptions). Never rebuild a scene; generated prefabs, tiles and palettes carry fingerprints so tools skip hand edits.
- Tilesets use Unity's own tiles: 2x2 Auto Tile (16 pieces) for organic areas, 3x3 (47 pieces) for anything that works one tile at a time.
- Art lives outside git, in the art package (see README.md). One-off scripts go in `tmp/`.
