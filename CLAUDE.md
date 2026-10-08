# Working on Bellwort Burrow with Claude

Indie project, worked on from 2 or 3 PCs a week. Keep usage low: do the cheap thing by default and save the expensive checks for when they can catch something.

## Session rhythm

- **Start (once per session, on this PC):** `git pull`. If new art was shared, download the newest zip from Drive into `ArtPackage/` and run Bellwort Burrow > Art Package > Check And Install.
- **During:** commit locally after each finished step. No pushes, pull request edits, CI checks or Drive uploads mid-session.
- **End (when Jocelyn says the session is done):** run the checks below that match what changed, then push once and update the pull request once. If art changed, ask whether the round is done before Check And Export and the Drive upload. If the art continues on another PC next, export anyway so that PC can install it.
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

## Looking at Unity cheaply

- Edit files directly on the PC through its shell instead of copying them through the cloud.
- Prefer files and logs over screenshots. Take screenshots at half scale and zoom only into the part you need.
- If the Unity MCP server is connected to the session, use it for console logs and menu items instead of driving the editor.

## Standing rules

- Everything in scenes is placed by hand (only backgrounds and skyboxes are exceptions). Never rebuild a scene; generated prefabs, tiles and palettes carry fingerprints so tools skip hand edits.
- Tilesets use Unity's own tiles: 2x2 Auto Tile (16 pieces) for organic areas, 3x3 (47 pieces) for anything that works one tile at a time.
- Art lives outside git, in the art package (see README.md). One-off scripts go in `tmp/`.
