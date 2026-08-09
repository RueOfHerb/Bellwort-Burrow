# Design/

This folder is the canonical, version-controlled record of Bellwort Burrow's world, story, and loose ideas. It lives outside `Assets/` on purpose — it isn't a Unity asset, so it doesn't need `.meta` files and won't get dragged into scene/prefab churn.

## Structure

- **`Ideas/inbox.md`** — fast capture. Anything new goes here first: dated, unfiltered, no editing for quality. The inbox's only job is making sure nothing gets lost.
- **`Lore/`** — setting-level canon: `world-bible.md` for the premise/tone/where-and-when, `locations/` (one file per named place), `factions/` (groups/institutions, if any exist).
- **`Story/`** — narrative: `main-arc.md` for the throughline and seasonal beats, `characters/` (one file per NPC once they're canon).

## Workflow

1. **Capture fast, triage later.** Write it in `Ideas/inbox.md` the moment it occurs to you, dated. Don't second-guess it there — that's how good ideas get refined away before they exist anywhere durable.
2. **Promote, don't duplicate.** When an idea is used or solidifies, move it out of the inbox into the right `Lore/` or `Story/` file and delete it from the inbox. Each fact should have exactly one home.
3. **Cross-link by name, not restatement.** A character file references a location by name rather than re-explaining it. One source of truth per fact.
4. **Anchor character files to their in-engine id.** Each file under `Story/characters/` should name the `NpcDefinition` id it corresponds to (see `Assets/Scripts/Data/NpcDefinition.cs`), so the fiction and the data asset don't drift apart as the roster grows.
5. **Periodic contradiction pass.** Every so often, reread `Lore/world-bible.md` against the newer character/location files and reconcile anything that's drifted.
