using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BellwortBurrow.Tiles;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace BellwortBurrow.EditorTools
{
    /// <summary>
    /// Makes the ground tiles you paint with, in Assets/Art/Tiles/, from each zone's ground sheet (<see cref="Sheets"/>:
    /// forest_ground makes the Forest tiles, burrow_ground the Burrow tiles, faerie_field_ground the Faerie Field tiles).
    /// Every ground sheet has the same slices, and
    /// each makes:
    /// - Path and Deer Trail (Forest Path, Forest Deer Trail): Rule Tiles that read all eight neighbours. Paint where the
    ///   path goes, any width down to one tile, and each tile picks its piece from which neighbours are painted. The
    ///   edge sits a couple of pixels inside the painted tiles, so every tile you paint shows. Paint a staircase and the
    ///   steps become a slope: a corner whose next step carries on diagonally is cut from tile corner to tile corner.
    ///   Every ground set is whole tiles only: there are no half tiles (they were cut on Oct 24, 2026; Jocelyn asked to
    ///   "go with just a 3x3 tilemap with 45 degree slices"), so a path bends a whole tile at a time.
    /// - Grass (Forest Grass): a Rule Tile that picks one of the grass variations at random.
    /// A ground sheet with stone (<see cref="GroundSheet.Stone"/>: burrow_ground) also makes Flagstones (Burrow
    /// Flagstones), a set like the path's, with the paths' grass fringe for an edge.
    /// A ground sheet with water (<see cref="GroundSheet.Water"/>: burrow_ground) also makes Water (Burrow Water), a set
    /// like the path's: the water with its pebbled banks, the far one a wall.
    /// It also makes Fast Water (Burrow Fast Water, <see cref="FastWaterTile"/>), painted on the Water layer below a
    /// waterfall and in narrow spots: the calm water's pieces with streaks of current running down them, joining the
    /// calm water as one creek (<see cref="WaterTile"/>), and the Waterfall (Burrow Waterfall), painted over a bank face
    /// where the creek runs off the edge (<see cref="WaterfallPiece"/>), its curtain and foam animated from the
    /// waterfall_frame slices (art-4).
    /// A ground sheet with the farm's grounds (<see cref="GroundSheet.Farm"/>: faerie_field_ground) also makes Tillable
    /// Dirt, Farmland and Watered Farmland (<see cref="FarmSets"/>), sets like the path's; the tillable dirt's glints
    /// (its Glow pixels) go on top of it unlit (<see cref="GlintRuleTile"/>).
    /// A ground sheet with raised ground (<see cref="GroundSheet.Banks"/>: burrow_ground) also makes:
    /// - Bank Face and Cliff Face (Burrow Bank Face, Burrow Cliff Face): the earth or stone face under raised ground's
    ///   front edge (the raised ground itself is plain Grass), painted three rows tall (four in a 45 degree column). They are Face Tiles (<see cref="FaceTile"/>), which
    ///   read their own column and their neighbours' to draw the lip and foot, step borders, 45 degree runs, inner
    ///   corner creases and open ends, each look in several variations (<see cref="FacePieceKeys"/>).
    ///   Mossy Cliff Face and Root Bank Face are the same faces overgrown: moss, hanging vines and ferns on the stone, the
    ///   great tree's roots through the earth.
    /// - Stone Steps and Log Steps (<see cref="Steps"/>): painted in place of the face cells they climb (<see cref="StepsTile"/>).
    /// These are ordinary Unity tile assets. Open one in the Inspector to see or change its rules. Running this again
    /// refreshes them in place, so scenes painted with them keep working; a tile you changed by hand is left alone
    /// (see <see cref="GeneratedAssets"/>).
    ///
    /// Every ground set is painted together on the sheet as one patch, from <see cref="Template"/>: each painted ('#')
    /// cell of the slice is a piece, and its layout is read from its neighbours in the template, exactly as the Rule
    /// Tile reads them in a scene. The template holds all 83 layouts (<see cref="Layout"/>); a layout that comes up
    /// more than once gives the tile several pieces to pick from at random. tools/ground_sets.py reads the template too.
    /// </summary>
    public static class GroundAutoTiles
    {
        public const string Folder = ArtLibrary.ArtRoot + "/Tiles";

        /// <summary>A zone's ground sheet: the tiles named after the zone are made from it, and its tile palette.</summary>
        public sealed class GroundSheet
        {
            /// <summary>The sheet's file name, like "forest_ground".</summary>
            public string Sheet;
            /// <summary>What its tiles are named after: "Forest" makes Forest Grass, Forest Path and so on.</summary>
            public string Zone;
            /// <summary>The tile palette's name, like "Forest Ground".</summary>
            public string Palette;
            /// <summary>
            /// Whether the sheet has raised ground: the face sets (<see cref="Faces"/>), which make the Bank Face and
            /// Cliff Face tiles.
            /// </summary>
            public bool Banks;
            /// <summary>Whether the sheet has flagstones laid like a path (<see cref="StoneSets"/>: burrow_ground).</summary>
            public bool Stone;
            /// <summary>Whether the sheet has water laid like a path (<see cref="WaterSets"/>: burrow_ground, faerie_field_ground), with Fast Water and the Waterfall.</summary>
            public bool Water;
            /// <summary>Whether the sheet has the farm's grounds (<see cref="FarmSets"/>: faerie_field_ground).</summary>
            public bool Farm;
            /// <summary>
            /// Whether its tiles pick each of their pieces about equally often, far apart on the noise so neighbours
            /// seldom match (<see cref="EvenPick"/>: burrow_ground, art-2.40). Without it Unity's Perlin pick favours the
            /// middle of each list, which the forest's grass leans on to keep its plainest piece the commonest.
            /// </summary>
            public bool EvenPick;

            /// <summary>Where one of its tiles is saved, by kind ("Grass", "Path", "Deer Trail").</summary>
            public string Tile(string kind) => $"{Folder}/{Zone} {kind}.asset";
        }

        /// <summary>Every zone's ground sheet, in the order setup makes them.</summary>
        public static readonly GroundSheet[] Sheets =
        {
            new GroundSheet { Sheet = "forest_ground", Zone = "Forest", Palette = "Forest Ground" },
            new GroundSheet { Sheet = "burrow_ground", Zone = "Burrow", Palette = "Burrow Ground", Banks = true, Stone = true, Water = true, EvenPick = true },
            new GroundSheet { Sheet = "faerie_field_ground", Zone = "Faerie Field", Palette = "Faerie Field Ground", Water = true, Farm = true },
        };

        /// <summary>A ground laid like a path: its tile's kind, its set's slice and its extra full pieces.</summary>
        sealed class GroundSet
        {
            public string Kind, Slice;
            public string[] Fills; // extra full pieces, picked at random alongside the template's full pieces
            /// <summary>Blocks the player: its tiles collide by their sprites' traced shapes (the water).</summary>
            public bool Blocks;
            /// <summary>Its pieces carry glints on the Glow layer: it's a <see cref="GlintRuleTile"/> (the tillable dirt).</summary>
            public bool Glints;
        }

        /// <summary>The grounds every ground sheet has, in the order the tile palette lays them out.</summary>
        static readonly GroundSet[] Sets =
        {
            new GroundSet { Kind = "Path", Slice = "path_set", Fills = new[] { "path_fill_00", "path_fill_01", "path_fill_more_00", "path_fill_more_01", "path_fill_more_02", "path_fill_more_03" } },
            new GroundSet { Kind = "Deer Trail", Slice = "trail_set", Fills = new[] { "trail_00", "trail_01" } },
        };

        /// <summary>
        /// A sheet with stone (<see cref="GroundSheet.Stone"/>) also lays flagstones as a path is laid: Flagstones, from
        /// flag_set, with the stone_flag tiles as extra full pieces.
        /// </summary>
        static readonly GroundSet[] StoneSets =
        {
            new GroundSet { Kind = "Flagstones", Slice = "flag_set",
                            Fills = new[] { "stone_flag_00", "stone_flag_01", "stone_flag_02", "stone_flag_03" } },
        };

        /// <summary>
        /// A sheet with water (<see cref="GroundSheet.Water"/>) also lays water as a path is laid: Water, from water_set,
        /// so a creek or a river of any width bends a tile at a time and cuts 45 degrees on a staircase. The far bank's
        /// wall and the stones along the waterline are drawn in each piece.
        /// </summary>
        static readonly GroundSet[] WaterSets =
        {
            new GroundSet { Kind = "Water", Slice = "water_set", Fills = new string[0], Blocks = true },
        };

        /// <summary>
        /// A sheet with the farm's grounds (<see cref="GroundSheet.Farm"/>) also lays, as a path is laid: Tillable Dirt,
        /// the only ground the player can till, flat with a speck that glints here and there (till_set, a
        /// <see cref="GlintRuleTile"/>), and over it Farmland, tilled, and Watered Farmland (farmland_set, watered_set).
        /// </summary>
        static readonly GroundSet[] FarmSets =
        {
            new GroundSet { Kind = "Tillable Dirt", Slice = "till_set", Glints = true,
                            Fills = Enumerable.Range(0, 10).Select(i => $"till_fill_{i:00}").ToArray() },
            new GroundSet { Kind = "Farmland", Slice = "farmland_set", Fills = new[] { "farmland_fill_00", "farmland_fill_01" } },
            new GroundSet { Kind = "Watered Farmland", Slice = "watered_set", Fills = new[] { "watered_fill_00", "watered_fill_01" } },
        };

        /// <summary>The grounds laid like a path a sheet has, in the order its tile palette lays them out.</summary>
        static IEnumerable<GroundSet> SetsOf(GroundSheet ground)
        {
            foreach (var set in Sets) yield return set;
            if (ground.Stone) foreach (var set in StoneSets) yield return set;
            if (ground.Water) foreach (var set in WaterSets) yield return set;
            if (ground.Farm) foreach (var set in FarmSets) yield return set;
        }

        /// <summary>Raised ground's faces: each kind and its set of pieces (<see cref="FacePieceKeys"/>).</summary>
        internal static readonly (string kind, string slice)[] Faces =
        {
            ("Bank Face", "bank_face_set"),
            ("Cliff Face", "cliff_face_set"),
            ("Mossy Cliff Face", "mossy_cliff_face_set"),
            ("Root Bank Face", "root_bank_face_set"),
        };

        /// <summary>Which faces join up as one face where they meet (<see cref="FaceTile.family"/>), so one paints into the other.</summary>
        internal static string FamilyOf(string kind) => kind.Contains("Bank") ? "bank" : kind.Contains("Cliff") ? "cliff" : "";

        /// <summary>
        /// Faces whose looks are drawn as one strip along the face (<see cref="FaceTile.stripLooks"/>): the root bank's
        /// roots run across the seams between its plain pieces.
        /// </summary>
        internal static readonly string[] StripFaces = { "Root Bank Face" };

        /// <summary>
        /// Steps cut up through a bank or cliff: each kind and its set, 4 by 3 tiles, rows top, middle and bottom, each
        /// row the left end, the middle, the right end and a single (<see cref="StepsTile"/>).
        /// </summary>
        internal static readonly (string kind, string slice)[] Steps =
        {
            ("Stone Steps", "stone_steps_set"),
            ("Log Steps", "log_steps_set"),
        };

        /// <summary>A ramp cut into a bank or cliff (<see cref="RampTile"/>): its slice, one picture of the whole ramp, and how it climbs.</summary>
        internal struct Ramp
        {
            public string kind, slice;
            public int columns, rows, landing;
            public Vector2 uphill;
            public float steepness, lift;

            public Ramp(string kind, string slice, int columns, int rows, Vector2 uphill, float steepness, float lift, int landing)
            {
                this.kind = kind; this.slice = slice; this.columns = columns; this.rows = rows;
                this.uphill = uphill; this.steepness = steepness; this.lift = lift; this.landing = landing;
            }
        }

        /// <summary>
        /// The ramps, earth with log edges cut into a bank and stone with stone curbs cut into a cliff, each climbing the
        /// face's 3 rows with a walkway 3 tiles wide between its edges, as wide as the steps: north straight into the hill
        /// (4 by 7 cells, the cut 3 and a half tiles wide in the middle, 4 tiles of run), and east and west along the face
        /// (6 by 7 cells, cut 3 and a half tiles in: a level landing at the low end where the mouth opens, then 5 tiles of
        /// slope).
        /// </summary>
        internal static readonly Ramp[] Ramps =
        {
            new Ramp("Earth Ramp North", "earth_ramp_n", 4, 7, Vector2.up, 0.75f, 0f, 0),
            new Ramp("Earth Ramp East", "earth_ramp_e", 6, 7, Vector2.right, 0.6f, 0.6f, 1),
            new Ramp("Earth Ramp West", "earth_ramp_w", 6, 7, Vector2.left, 0.6f, 0.6f, 1),
            new Ramp("Stone Ramp North", "stone_ramp_n", 4, 7, Vector2.up, 0.75f, 0f, 0),
            new Ramp("Stone Ramp East", "stone_ramp_e", 6, 7, Vector2.right, 0.6f, 0.6f, 1),
            new Ramp("Stone Ramp West", "stone_ramp_w", 6, 7, Vector2.left, 0.6f, 0.6f, 1),
        };


        /// <summary>The face sets' slices: 17 by 16 tiles, the pieces laid in <see cref="FacePieceKeys"/> order, 17 to a row.</summary>
        public const int FaceSetColumns = 17, FaceSetRows = 16;

        /// <summary>
        /// Every face piece's key, in the order the face sets lay them out (tools/face_pieces.py lays them the same way;
        /// see <see cref="FaceTile.keys"/> for what a key means). A key that comes up more than once has variations: a
        /// flat face carrying on both ways has 12, a 45 degree column 4, everything else 2. Change it only together with
        /// the sheets.
        /// </summary>
        public static List<string> FacePieceKeys()
        {
            var keys = new List<string>();
            foreach (char row in "TMBS")
                foreach (char west in "cobk")
                    foreach (char east in "cobk")
                    {
                        int n = west == 'c' && east == 'c' ? 12 : 2;
                        for (int v = 0; v < n; v++) keys.Add($"F{row}{west}{east}");
                    }
            foreach (char shape in "DU")
                for (int place = 0; place < 4; place++)
                    foreach (var (west, east) in new[] { ('c', 'c'), ('o', 'c'), ('c', 'o') })
                    {
                        int n = west == 'c' && east == 'c' ? 4 : 2;
                        for (int v = 0; v < n; v++) keys.Add($"{shape}{(char)('0' + place)}{west}{east}");
                    }
            return keys;
        }

        /// <summary>
        /// The patch every 3x3 ground set is painted from, top row first: '#' is a painted tile, '.' is not. Outside
        /// it counts as not painted. Change it only together with the sheet's slices.
        /// </summary>
        public static readonly string[] Template =
        {
            "##...##..#..##.##",
            "#...###.###.###.#",
            "####.#.##.######.",
            "...####.##.####.#",
            "#.##.#....#..###.",
            "#.#####...##..#.#",
            "...###.##..#.####",
            ".########.######.",
            "##.##.#.##.###...",
            "#.##.##.##..#..##",
        };

        /// <summary>How many layouts a ground set has: the 47 of Unity's 3x3 Auto Tile plus 36 slope pieces.</summary>
        public const int LayoutCount = 83;

        /// <summary>The layout every tile surrounded by painted tiles has (all nine bits).</summary>
        public const uint Full = 511;

        // Neighbour bits, y up: top row NW 64, N 128, NE 256; middle W 8, the tile 16, E 32; bottom SW 1, S 2, SE 4.
        const uint N = 128, S = 2, W = 8, E = 32, NW = 64, NE = 256, SW = 1, SE = 4;

        // The Rule Tile's neighbour order (its default positions), as bits.
        static readonly uint[] RuleOrder = { NW, N, NE, W, E, SW, S, SE };
        static readonly Vector3Int[] RulePositions =
        {
            new Vector3Int(-1, 1, 0), new Vector3Int(0, 1, 0), new Vector3Int(1, 1, 0),
            new Vector3Int(-1, 0, 0), new Vector3Int(1, 0, 0),
            new Vector3Int(-1, -1, 0), new Vector3Int(0, -1, 0), new Vector3Int(1, -1, 0),
        };

        // Perlin noise favors the middle of the list, so the plainest grass goes in the middle (a sheet with EvenPick
        // takes them all about equally). grass_more is the Burrow's eight more (art-2.40); a sheet without it skips them.
        static readonly string[] GrassPieces = { "grass_00", "grass_01", "grass_02", "grass_03",
            "grass_more_00", "grass_more_01", "grass_more_02", "grass_more_03", "grass_more_04", "grass_more_05", "grass_more_06", "grass_more_07" };

        [MenuItem("Bellwort Burrow/Setup/Forest Steps/4. Make Ground Auto Tiles", false, 204)]
        static void MakeFromMenu()
        {
            if (!ArtLibrary.CheckArtInstalled()) return;
            Debug.Log($"Bellwort Burrow: made or refreshed {MakeAll()} ground tiles in {Folder}.");
        }

        /// <summary>Makes or refreshes every ground tile of every installed ground sheet. Returns how many.</summary>
        public static int MakeAll()
        {
            int made = 0;
            var missing = new List<string>();
            foreach (var ground in Sheets)
            {
                string sheetPath = ArtLibrary.FindSheet(ground.Sheet);
                if (sheetPath == null) missing.Add(ground.Sheet + ".aseprite");
                else made += Make(ground, sheetPath);
            }
            if (missing.Count == Sheets.Length)
                Debug.LogError($"Bellwort Burrow: couldn't find any ground sheet ({string.Join(", ", missing)}) under {ArtLibrary.ArtRoot}.");
            else if (missing.Count > 0)
                Debug.LogWarning($"Bellwort Burrow: skipped the tiles of {string.Join(", ", missing)}, which isn't under {ArtLibrary.ArtRoot}.");
            return made;
        }

        /// <summary>Makes or refreshes one ground sheet's tiles. Returns how many.</summary>
        static int Make(GroundSheet ground, string sheetPath)
        {
            var sprites = ArtLibrary.LoadSubAssets<Sprite>(sheetPath);
            ArtLibrary.EnsureFolder(Folder);

            var written = new List<string>();
            var kept = new List<string>();
            foreach (var set in SetsOf(ground))
            {
                string setPath = ground.Tile(set.Kind);
                if (!GeneratedAssets.CanOverwrite(setPath)) kept.Add(setPath);
                else if (MakeSetTile(ground, set, sprites, setPath)) written.Add(setPath);
            }
            if (ground.Water)
            {
                string fastPath = ground.Tile(FastWaterKind);
                if (!GeneratedAssets.CanOverwrite(fastPath)) kept.Add(fastPath);
                else if (MakeFastWaterTile(ground, sheetPath, sprites, fastPath)) written.Add(fastPath);
                LinkWater(ground);
                string fallPath = ground.Tile("Waterfall");
                if (!GeneratedAssets.CanOverwrite(fallPath)) kept.Add(fallPath);
                else if (MakeWaterfallTile(ground, sprites, fallPath)) written.Add(fallPath);
            }
            string grassPath = ground.Tile("Grass");
            if (!GeneratedAssets.CanOverwrite(grassPath)) kept.Add(grassPath);
            else if (MakeGrassTile(ground, sprites, grassPath)) written.Add(grassPath);
            if (ground.Banks)
            {
                foreach (var (kind, slice) in Faces)
                {
                    string facePath = ground.Tile(kind);
                    if (!GeneratedAssets.CanOverwrite(facePath)) kept.Add(facePath);
                    else if (MakeFaceTile<FaceTile>(ground, slice, sprites, facePath, t => { t.stripLooks = StripFaces.Contains(kind); t.family = FamilyOf(kind); })) written.Add(facePath);
                }
                foreach (var (kind, slice) in Steps)
                {
                    string stepsPath = ground.Tile(kind);
                    if (!GeneratedAssets.CanOverwrite(stepsPath)) kept.Add(stepsPath);
                    else if (MakeStepsTile(ground, slice, sprites, stepsPath)) written.Add(stepsPath);
                }
                foreach (var ramp in Ramps)
                {
                    string rampPath = ground.Tile(ramp.kind);
                    if (!GeneratedAssets.CanOverwrite(rampPath)) kept.Add(rampPath);
                    else if (MakeRampTile(ground, ramp, sprites, rampPath)) written.Add(rampPath);
                }
            }

            AssetDatabase.SaveAssets();
            foreach (var path in written) GeneratedAssets.Stamp(path);
            GeneratedAssets.ReportKept("Make Ground Auto Tiles", kept);
            return written.Count;
        }

        /// <summary>A ground sheet's tiles, in the order the painting tiles sit in its tile palette.</summary>
        public static IEnumerable<string> TilePaths(GroundSheet ground)
        {
            yield return ground.Tile("Grass");
            foreach (var set in SetsOf(ground))
            {
                yield return ground.Tile(set.Kind);
                if (set.Kind == "Water") yield return ground.Tile(FastWaterKind);
            }
            if (ground.Banks)
            {
                foreach (var (kind, _) in Faces) yield return ground.Tile(kind);
                foreach (var (kind, _) in Steps) yield return ground.Tile(kind);
                foreach (var ramp in Ramps) yield return ground.Tile(ramp.kind);
            }
            if (ground.Water) yield return ground.Tile("Waterfall");
        }

        /// <summary>
        /// The ground sets painted from <see cref="Template"/>, in the order the tile palette lays them out: each one's
        /// slice and the extra full pieces it picks from (see <see cref="GroundPalette"/>). Every ground sheet has the
        /// path and deer trail; a sheet with stone has the flagstones too, and one with water the water.
        /// </summary>
        internal static IEnumerable<(string slice, string[] fills)> TemplateSets(GroundSheet ground)
        {
            foreach (var set in SetsOf(ground)) yield return (set.Slice, set.Fills);
        }

        /// <summary>
        /// Which corner neighbours change a tile's piece, given which sides are painted. A corner counts when both
        /// sides next to it are painted (an inner corner, as in Unity's Auto Tile), and also when it decides a slope:
        /// for an outer corner (both its sides empty), the neighbour past each end of that corner, along a painted
        /// side, says whether the edge carries on diagonally there.
        /// </summary>
        internal static uint RelevantCorners(bool n, bool s, bool w, bool e)
        {
            uint r = 0;
            if ((n && w) || (!n && !e && w) || (!s && !w && n)) r |= NW;
            if ((n && e) || (!n && !w && e) || (!s && !e && n)) r |= NE;
            if ((s && w) || (!s && !e && w) || (!n && !w && s)) r |= SW;
            if ((s && e) || (!s && !w && e) || (!n && !e && s)) r |= SE;
            return r;
        }

        /// <summary>
        /// A tile's layout from all eight neighbours (bits as above, 16 for the tile itself): the painted sides, plus
        /// the painted corners that matter (<see cref="RelevantCorners"/>). 83 layouts in all.
        /// </summary>
        internal static uint LayoutOf(uint neighbours)
        {
            bool n = (neighbours & N) != 0, s = (neighbours & S) != 0, w = (neighbours & W) != 0, e = (neighbours & E) != 0;
            uint layout = 16 | (neighbours & (N | S | W | E));
            return layout | (neighbours & RelevantCorners(n, s, w, e));
        }

        /// <summary>A painted template cell's neighbours (bits as above, 16 for the cell itself).</summary>
        internal static uint Neighbours(string[] template, int column, int row)
        {
            bool Painted(int dx, int dy)
            {
                int x = column + dx, y = row + dy;
                return y >= 0 && y < template.Length && x >= 0 && x < template[y].Length && template[y][x] == '#';
            }
            uint around = 16;
            if (Painted(0, -1)) around |= N;
            if (Painted(0, 1)) around |= S;
            if (Painted(-1, 0)) around |= W;
            if (Painted(1, 0)) around |= E;
            if (Painted(-1, -1)) around |= NW;
            if (Painted(1, -1)) around |= NE;
            if (Painted(-1, 1)) around |= SW;
            if (Painted(1, 1)) around |= SE;
            return around;
        }

        /// <summary>The layout of a painted template cell.</summary>
        internal static uint Layout(string[] template, int column, int row)
        {
            bool Painted(int dx, int dy)
            {
                int x = column + dx, y = row + dy;
                return y >= 0 && y < template.Length && x >= 0 && x < template[y].Length && template[y][x] == '#';
            }
            uint around = 16;
            if (Painted(0, -1)) around |= N;
            if (Painted(0, 1)) around |= S;
            if (Painted(-1, 0)) around |= W;
            if (Painted(1, 0)) around |= E;
            if (Painted(-1, -1)) around |= NW;
            if (Painted(1, -1)) around |= NE;
            if (Painted(-1, 1)) around |= SW;
            if (Painted(1, 1)) around |= SE;
            return LayoutOf(around);
        }

        /// <summary>
        /// The Rule Tile neighbour conditions for a layout, in the Rule Tile's order (NW, N, NE, W, E, SW, S, SE):
        /// This where the layout needs that neighbour painted, NotThis where it needs it empty, 0 where it doesn't care.
        /// </summary>
        internal static int[] RuleNeighbours(uint layout)
        {
            bool n = (layout & N) != 0, s = (layout & S) != 0, w = (layout & W) != 0, e = (layout & E) != 0;
            uint matters = N | S | W | E | RelevantCorners(n, s, w, e);
            var conditions = new int[8];
            for (int i = 0; i < 8; i++)
            {
                uint bit = RuleOrder[i];
                if ((matters & bit) == 0) conditions[i] = 0;
                else conditions[i] = (layout & bit) != 0 ? RuleTile.TilingRuleOutput.Neighbor.This : RuleTile.TilingRuleOutput.Neighbor.NotThis;
            }
            return conditions;
        }

        /// <summary>Sprite name of a template cell: the importer numbers a slice's tiles row by row from the top left.</summary>
        internal static string PieceName(string slice, int column, int row, string[] template)
        {
            int columns = template[0].Length, count = columns * template.Length;
            string digits = new string('0', Mathf.Max(2, count.ToString().Length));
            return slice + "_" + (row * columns + column).ToString(digits);
        }

        /// <summary>How a set's tiles collide: by their sprites' traced shapes when it blocks the player, else not at all.</summary>
        internal static Tile.ColliderType ColliderFor(bool blocks) => blocks ? Tile.ColliderType.Sprite : Tile.ColliderType.None;

        /// <summary>A Rule Tile rule: these neighbour conditions (<see cref="RuleNeighbours"/>) show one of these pieces.</summary>
        static RuleTile.TilingRule LayoutRule(int[] conditions, List<Sprite> pieces, Tile.ColliderType collider) => new RuleTile.TilingRule
        {
            m_Neighbors = new List<int>(conditions),
            m_NeighborPositions = new List<Vector3Int>(RulePositions),
            m_Sprites = pieces.ToArray(),
            m_Output = pieces.Count > 1 ? RuleTile.TilingRuleOutput.OutputSprite.Random : RuleTile.TilingRuleOutput.OutputSprite.Single,
            m_PerlinScale = 0.5f,
            m_ColliderType = collider,
            m_RuleTransform = RuleTile.TilingRuleOutput.Transform.Fixed,
        };

        /// <summary>
        /// Makes a ground set's tile, like Forest Path: a Rule Tile with a rule per layout the set has, each showing the
        /// pieces painted with that layout in the set's slice (the fills join the full layout's pieces).
        /// </summary>
        static bool MakeSetTile(GroundSheet ground, GroundSet set, Dictionary<string, Sprite> sprites, string path)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            var pieces = new Dictionary<uint, List<Sprite>>();
            int missing = 0;
            for (int row = 0; row < Template.Length; row++)
                for (int column = 0; column < Template[row].Length; column++)
                {
                    if (Template[row][column] != '#') continue;
                    if (!sprites.TryGetValue(PieceName(set.Slice, column, row, Template), out var piece)) { missing++; continue; }
                    uint layout = Layout(Template, column, row);
                    if (!pieces.TryGetValue(layout, out var l)) pieces[layout] = l = new List<Sprite>();
                    l.Add(piece);
                }
            if (!pieces.ContainsKey(Full))
            {
                Debug.LogWarning($"Bellwort Burrow: skipped {name}; {ground.Sheet} has no {set.Slice} slice painted from the template.");
                return false;
            }
            foreach (var fill in set.Fills)
                if (sprites.TryGetValue(fill, out var sprite)) pieces[Full].Add(sprite);
            if (missing > 0)
                Debug.LogWarning($"Bellwort Burrow: {name}: {missing} tiles of the template have no piece in {set.Slice}. " +
                                 "Is the slice the size of GroundAutoTiles.Template and painted from it?");
            if (pieces.Count < LayoutCount)
                Debug.LogWarning($"Bellwort Burrow: {name} has {pieces.Count} of the {LayoutCount} layouts; tiles with a missing layout show the full piece.");

            var across = WangPieces(sprites, "path_across");
            var up = WangPieces(sprites, "path_up");
            bool wang = set.Kind == "Path" && across != null && up != null;
            var tile = set.Glints ? ScriptableObject.CreateInstance<GlintRuleTile>()
                     : wang ? ScriptableObject.CreateInstance<WangPathTile>()
                     : set.Kind == "Water" ? ScriptableObject.CreateInstance<WaterTile>()
                     : ScriptableObject.CreateInstance<RuleTile>();
            tile.name = name;
            if (wang) { ((WangPathTile)tile).across = across; ((WangPathTile)tile).up = up; }
            if (tile is GlintRuleTile glinting)
            {
                glinting.glintObject = GlintObject();
                foreach (var piece in pieces.Values.SelectMany(p => p))
                    if (sprites.TryGetValue(piece.name + "_glow", out var glint)) { glinting.glintPieces.Add(piece); glinting.glints.Add(glint); }
            }
            tile.m_DefaultSprite = pieces[Full][0];
            var collider = ColliderFor(set.Blocks);
            tile.m_DefaultColliderType = collider;
            tile.m_TilingRules = new List<RuleTile.TilingRule>();
            foreach (var kv in pieces.OrderBy(kv => kv.Key)) tile.m_TilingRules.Add(LayoutRule(RuleNeighbours(kv.Key), kv.Value, collider));
            // The Rule Tile picks a random piece from Perlin noise at the cell times the scale: at 0.5 every other cell
            // lands on the noise's grid and takes the same piece, which would set the glints out in a grid.
            if (set.Glints) foreach (var rule in tile.m_TilingRules) rule.m_PerlinScale = GlintPerlinScale;
            if (ground.EvenPick)
                foreach (var rule in tile.m_TilingRules)
                {
                    if (rule.m_Output != RuleTile.TilingRuleOutput.OutputSprite.Random) continue;
                    if (!set.Glints) rule.m_PerlinScale = ScatterPerlinScale;
                    rule.m_Sprites = EvenPick(rule.m_Sprites.Distinct().ToList(), rule.m_PerlinScale);
                }
            SaveKeepingId(tile, path);
            return true;
        }

        /// <summary>The Perlin scale a glinting set picks its pieces with: off the noise's grid, so glints scatter.</summary>
        internal const float GlintPerlinScale = 0.37f;

        /// <summary>
        /// A Wang path's pieces from a slice of WangPathTile.States squared tiles (path_across, path_up: art-2.41), or
        /// null when the sheet doesn't have them all.
        /// </summary>
        static Sprite[] WangPieces(Dictionary<string, Sprite> sprites, string slice)
        {
            var pieces = new Sprite[WangPathTile.States * WangPathTile.States];
            for (int i = 0; i < pieces.Length; i++)
                if (!sprites.TryGetValue($"{slice}_{i:00}", out pieces[i])) return null;
            return pieces;
        }

        /// <summary>How often a piece comes up against the others in its list (art-2.41): the featured pieces
        /// (grass_more, path_fill_more, with clumps, ruts and big stones) half as often as the plain ones, so the
        /// features read as now and then instead of everywhere.</summary>
        internal static float WeightOf(Sprite piece) =>
            piece != null && (piece.name.StartsWith("grass_more") || piece.name.StartsWith("path_fill_more")) ? 1f : 2f;

        /// <summary>The Perlin scale a sheet with EvenPick picks its pieces with: far enough along the noise from one
        /// cell to the next that neighbours seldom land on the same piece, and off its grid.</summary>
        internal const float ScatterPerlinScale = 7.31f;

        /// <summary>
        /// The pieces laid out so a Rule Tile's Perlin pick takes each about equally often (art-2.40). Unity picks a
        /// piece by Perlin noise, which bunches round its middle: at 0.5, one of twelve grass pieces covered 45% of a
        /// map and the first and last almost never came up. Here the list is cut into slots, and each piece gets a run
        /// of them as wide as an equal share of where the noise actually falls at this scale.
        /// </summary>
        internal static Sprite[] EvenPick(IList<Sprite> pieces, float scale, int slots = 120) =>
            EvenPick(pieces, pieces.Select(WeightOf).ToList(), scale, slots);

        /// <summary>As EvenPick, each piece coming up in proportion to its weight.</summary>
        internal static Sprite[] EvenPick(IList<Sprite> pieces, IList<float> weights, float scale, int slots = 120)
        {
            if (pieces.Count < 2) return pieces.ToArray();
            float weightTotal = weights.Sum();
            var upTo = new float[pieces.Count];   // where each piece's share of the noise ends, 0 to 1
            for (int i = 0, n = pieces.Count; i < n; i++) upTo[i] = (i > 0 ? upTo[i - 1] : 0f) + weights[i] / weightTotal;
            var mass = new float[slots];
            for (int y = 0; y < 128; y++)
                for (int x = 0; x < 128; x++)
                {
                    float v = Mathf.PerlinNoise((x + 100000f) * scale, (y + 100000f) * scale);   // as RuleTile samples it
                    mass[Mathf.Clamp(Mathf.FloorToInt(v * slots), 0, slots - 1)]++;
                }
            float total = mass.Sum(), soFar = 0f;
            var laid = new Sprite[slots];
            for (int i = 0; i < slots; i++)
            {
                float middle = soFar + mass[i] / 2f;
                float at = middle / total;
                int k = 0;
                while (k < pieces.Count - 1 && at > upTo[k]) k++;
                laid[i] = pieces[k];
                soFar += mass[i];
            }
            return laid;
        }

        /// <summary>Where the glint a <see cref="GlintRuleTile"/> puts on a cell is kept.</summary>
        public const string GlintObjectPath = Folder + "/Tile Glint.prefab";

        /// <summary>
        /// The glint a <see cref="GlintRuleTile"/> puts on a cell: an empty SpriteRenderer on the unlit material (the tile
        /// gives it the cell's glint picture and its sorting), made once at <see cref="GlintObjectPath"/>.
        /// </summary>
        static GameObject GlintObject()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(GlintObjectPath);
            if (existing != null) return existing;
            var go = new GameObject("Tile Glint");
            var picture = go.AddComponent<SpriteRenderer>();
            if (ArtLibrary.UnlitMaterial != null) picture.sharedMaterial = ArtLibrary.UnlitMaterial;
            var saved = PrefabUtility.SaveAsPrefabAsset(go, GlintObjectPath);
            Object.DestroyImmediate(go);
            return saved;
        }

        /// <summary>
        /// Saves a tile at path. When a tile of another kind is already there (the Auto Tiles these used to be), its
        /// file is replaced but its .meta is kept, so the asset keeps its ID and every scene painted with it stays linked.
        /// </summary>
        static void SaveKeepingId(TileBase tile, string path)
        {
            var existing = AssetDatabase.LoadMainAssetAtPath(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(tile, path);
                return;
            }
            if (existing.GetType() == tile.GetType())
            {
                EditorUtility.CopySerialized(tile, existing);
                existing.name = Path.GetFileNameWithoutExtension(path);
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(tile);
                return;
            }
            string temp = $"{Path.GetDirectoryName(path).Replace('\\', '/')}/{Path.GetFileNameWithoutExtension(path)} (rebuilding).asset";
            AssetDatabase.CreateAsset(tile, temp);
            AssetDatabase.SaveAssets();
            File.Copy(temp, path, true);
            AssetDatabase.DeleteAsset(temp);
            // The copy still carries the temporary file's name inside it.
            string name = Path.GetFileNameWithoutExtension(path);
            File.WriteAllText(path, File.ReadAllText(path).Replace($"m_Name: {name} (rebuilding)", $"m_Name: {name}"));
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            // Tilemaps keep calling the old kind's code for an asset whose kind changed until scripts reload.
            EditorUtility.RequestScriptReload();
        }

        /// <summary>
        /// Makes a Bank Face or Cliff Face: a Face Tile holding every piece of the face set with its key
        /// (<see cref="FacePieceKeys"/>), the set's tiles read row by row from the top left.
        /// </summary>
        static bool MakeFaceTile<T>(GroundSheet ground, string slice, Dictionary<string, Sprite> sprites, string path, Action<T> setUp)
            where T : FaceTile
        {
            string name = Path.GetFileNameWithoutExtension(path);
            var all = FacePieceKeys();
            var keys = new List<string>();
            var pieces = new List<Sprite>();
            int count = FaceSetColumns * FaceSetRows, missing = 0;
            string digits = new string('0', Mathf.Max(2, count.ToString().Length));
            for (int i = 0; i < all.Count; i++)
            {
                if (!sprites.TryGetValue(slice + "_" + i.ToString(digits), out var piece)) { missing++; continue; }
                keys.Add(all[i]);
                pieces.Add(piece);
            }
            if (pieces.Count == 0)
            {
                Debug.LogWarning($"Bellwort Burrow: skipped {name}; {ground.Sheet} has no {slice} slice of face pieces.");
                return false;
            }
            if (missing > 0)
                Debug.LogWarning($"Bellwort Burrow: {name} lacks {missing} of its {all.Count} pieces; is {slice} {FaceSetColumns} by {FaceSetRows} tiles, laid out in FacePieceKeys order?");

            var tile = ScriptableObject.CreateInstance<T>();
            tile.name = name;
            tile.keys = keys.ToArray();
            tile.sprites = pieces.ToArray();
            int plain = keys.IndexOf("FMcc");
            if (plain < 0) plain = keys.IndexOf("FTcc");
            tile.defaultSprite = plain >= 0 ? pieces[plain] : pieces[0];
            setUp?.Invoke(tile);
            SaveKeepingId(tile, path);
            return true;
        }

        /// <summary>
        /// Makes Stone Steps or Log Steps: a Steps Tile holding the set's pieces, rows top, middle, bottom, each row left
        /// end, middle, right end, single (<see cref="StepsTile.PieceFor"/>).
        /// </summary>
        static bool MakeStepsTile(GroundSheet ground, string slice, Dictionary<string, Sprite> sprites, string path)
        {
            var template = SideRowTemplate;
            var pieces = new Sprite[StepsTile.PieceRows.Length * StepsTile.PieceSides.Length];
            int found = 0;
            foreach (char row in StepsTile.PieceRows)
                foreach (char side in StepsTile.PieceSides)
                    if (sprites.TryGetValue(PieceName(slice, StepsTile.PieceSides.IndexOf(side), StepsTile.PieceRows.IndexOf(row), template), out var p))
                    {
                        pieces[StepsTile.IndexOf(row, side)] = p;
                        found++;
                    }
            if (found == 0)
            {
                Debug.LogWarning($"Bellwort Burrow: skipped {Path.GetFileNameWithoutExtension(path)}; {ground.Sheet} has no {slice} slice.");
                return false;
            }
            var tile = ScriptableObject.CreateInstance<StepsTile>();
            tile.name = Path.GetFileNameWithoutExtension(path);
            tile.sprites = pieces;
            tile.steepness = 1f;
            SaveKeepingId(tile, path);
            return true;
        }

        /// <summary>Makes a ramp: a Ramp Tile holding its slice's pieces, row by row from the top, and how it climbs.</summary>
        static bool MakeRampTile(GroundSheet ground, Ramp ramp, Dictionary<string, Sprite> sprites, string path)
        {
            var template = Enumerable.Repeat(new string('#', ramp.columns), ramp.rows).ToArray();
            var pieces = new Sprite[ramp.columns * ramp.rows];
            int found = 0;
            for (int row = 0; row < ramp.rows; row++)
                for (int column = 0; column < ramp.columns; column++)
                    if (sprites.TryGetValue(PieceName(ramp.slice, column, row, template), out var p))
                    {
                        pieces[row * ramp.columns + column] = p;
                        found++;
                    }
            if (found == 0)
            {
                Debug.LogWarning($"Bellwort Burrow: skipped {Path.GetFileNameWithoutExtension(path)}; {ground.Sheet} has no {ramp.slice} slice.");
                return false;
            }
            var tile = ScriptableObject.CreateInstance<RampTile>();
            tile.name = Path.GetFileNameWithoutExtension(path);
            tile.columns = ramp.columns;
            tile.rows = ramp.rows;
            tile.sprites = pieces;
            tile.uphill = ramp.uphill;
            tile.steepness = ramp.steepness;
            tile.lift = ramp.lift;
            tile.landing = ramp.landing;
            tile.stretches = ramp.uphill == Vector2.up;     // the North ramps paint any width and length
            tile.faceRows = 3;
            SaveKeepingId(tile, path);
            return true;
        }

        /// <summary>The layout of a set in rows top, middle, bottom and sides left, middle, right, single: the waterfall, the steps, the ramp.</summary>
        static readonly string[] SideRowTemplate = { "####", "####", "####" };

        // ---- Waterfall ----------------------------------------------------------------------------------------------
        /// <summary>
        /// The waterfall set: a fall over a bank face, three rows tall like the face, its pieces in columns by side
        /// (the left end, the middle, the right end, a fall one tile wide) and rows top, middle, bottom.
        /// </summary>
        internal const string WaterfallSlice = "waterfall_set";
        internal const string WaterfallSides = "lmrs";   // left end, middle, right end, single (one tile wide)
        internal const string WaterfallRows = "TMB";

        /// <summary>
        /// Which waterfall piece a cell takes from whether the fall carries on above, below, west and east of it: the
        /// top row where nothing falls into it, the bottom where nothing is under it, the middle between (a fall one
        /// row tall takes the top); an end where the fall stops on that side.
        /// </summary>
        internal static (char row, char side) WaterfallPiece(bool n, bool s, bool w, bool e)
        {
            char row = !n ? 'T' : !s ? 'B' : 'M';
            char side = w && e ? 'm' : w ? 'r' : e ? 'l' : 's';
            return (row, side);
        }

        /// <summary>
        /// Makes the Waterfall (Burrow Waterfall): painted over the bank face where the creek runs off raised ground,
        /// on a Tilemap of its own above Banks, with the creek's Water painted straight on underneath. Each cell picks
        /// its piece from <see cref="WaterfallPiece"/>, one rule for each way its four sides can be.
        /// </summary>
        static bool MakeWaterfallTile(GroundSheet ground, Dictionary<string, Sprite> sprites, string path) =>
            MakeSideRowTile(ground, WaterfallSlice, sprites, path, WaterfallFrameSlices(sprites), WaterfallFramesPerSecond);

        /// <summary>The waterfall's later frames (art-4): waterfall_frame_1, _2 and so on beside waterfall_set, the first.</summary>
        internal const string WaterfallFramePrefix = "waterfall_frame_";

        /// <summary>The waterfall's frames a second: 110 ms a frame (Jocelyn's pick, Oct 29).</summary>
        internal const float WaterfallFramesPerSecond = 1000f / 110f;

        /// <summary>The waterfall's frame slices that the sheet has, in order, starting with waterfall_set.</summary>
        static List<string> WaterfallFrameSlices(Dictionary<string, Sprite> sprites)
        {
            var slices = new List<string> { WaterfallSlice };
            for (int i = 1; sprites.ContainsKey(PieceName(WaterfallFramePrefix + i, 1, 1, SideRowTemplate)); i++)
                slices.Add(WaterfallFramePrefix + i);
            return slices;
        }

        /// <summary>
        /// The slices that hold animation frames (the waterfall's later frames, the fast water's streaks): setup reads
        /// them for the animated tiles, and the tile palette leaves them out.
        /// </summary>
        internal static bool IsAnimationFrameSlice(string slice) =>
            slice != null && (slice.StartsWith(WaterfallFramePrefix, StringComparison.Ordinal) || slice == FastWaterFrames.StreaksSlice);

        // ---- Fast water ---------------------------------------------------------------------------------------------
        internal const string FastWaterKind = "Fast Water";

        /// <summary>Where a zone's fast water frames are saved: a texture with the frames as sprites on it.</summary>
        internal static string FastWaterFramesPath(GroundSheet ground) => $"{Folder}/{ground.Zone} Fast Water Frames.asset";

        /// <summary>
        /// Makes Fast Water (Burrow Fast Water, art-4): a rule per layout of the calm water's set, each showing that
        /// layout's first piece (which it also collides by) and animating the frames <see cref="FastWaterFrames"/>
        /// makes from it, one set of frames per look, a look picked by where the cell is.
        /// </summary>
        static bool MakeFastWaterTile(GroundSheet ground, string sheetPath, Dictionary<string, Sprite> sprites, string path)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            AsepriteSheet sheet;
            try { sheet = AsepriteSheet.Read(File.ReadAllBytes(sheetPath)); }
            catch (Exception e) { Debug.LogWarning($"Bellwort Burrow: skipped {name}; couldn't read {sheetPath}: {e.Message}"); return false; }
            int at = sheet.Slices.FindIndex(s => s.Name == "water_set");
            if (at < 0) { Debug.LogWarning($"Bellwort Burrow: skipped {name}; {ground.Sheet} has no water_set slice."); return false; }
            var water = sheet.Slices[at];
            var first = new SortedDictionary<uint, (int x, int y, Sprite piece)>();
            var full = new List<(int x, int y)>();
            for (int row = 0; row < Template.Length; row++)
                for (int column = 0; column < Template[row].Length; column++)
                {
                    if (Template[row][column] != '#') continue;
                    if (!sprites.TryGetValue(PieceName("water_set", column, row, Template), out var piece)) continue;
                    uint layout = Layout(Template, column, row);
                    int x = water.X + column * 16, y = water.Y + row * 16;
                    if (layout == Full) full.Add((x, y));
                    if (!first.ContainsKey(layout)) first[layout] = (x, y, piece);
                }
            if (!first.ContainsKey(Full)) { Debug.LogWarning($"Bellwort Burrow: skipped {name}; {ground.Sheet}'s water_set has no full piece."); return false; }
            var rules = first.Values.ToList();
            var made = FastWaterFrames.Make(sheet, rules, full, FastWaterFramesPath(ground));
            if (made == null) { Debug.LogWarning($"Bellwort Burrow: skipped {name}; {ground.Sheet} has no {FastWaterFrames.StreaksSlice} slice."); return false; }

            var tile = ScriptableObject.CreateInstance<FastWaterTile>();
            tile.name = name;
            var collider = ColliderFor(true);
            tile.m_DefaultSprite = first[Full].piece;
            tile.m_DefaultColliderType = collider;
            tile.m_TilingRules = new List<RuleTile.TilingRule>();
            foreach (var kv in first) tile.m_TilingRules.Add(LayoutRule(RuleNeighbours(kv.Key), new List<Sprite> { kv.Value.piece }, collider));
            tile.frames = made.Value.frames;
            tile.framesPerLook = made.Value.framesPerLook;
            tile.lookWeights = Enumerable.Repeat(FastWaterFrames.LookWeight, made.Value.looks).Append(FastWaterFrames.CalmWeight).ToArray();
            tile.framesPerSecond = FastWaterFrames.FramesPerSecond;
            SaveKeepingId(tile, path);
            return true;
        }

        /// <summary>Calm water and Fast Water count each other as the same water, so they run on as one creek.</summary>
        static void LinkWater(GroundSheet ground)
        {
            var calm = AssetDatabase.LoadAssetAtPath<WaterTile>(ground.Tile("Water"));
            var fast = AssetDatabase.LoadAssetAtPath<WaterTile>(ground.Tile(FastWaterKind));
            if (calm == null || fast == null) return;
            calm.sameWater = new TileBase[] { fast };
            fast.sameWater = new TileBase[] { calm };
            EditorUtility.SetDirty(calm);
            EditorUtility.SetDirty(fast);
        }

        /// <summary>
        /// A Rule Tile that picks its piece by whether it carries on above, below, west and east (<see cref="WaterfallPiece"/>),
        /// from a set laid out in rows top, middle, bottom and sides left end, middle, right end, single: the Waterfall
        /// and the steps' layout.
        /// </summary>
        static bool MakeSideRowTile(GroundSheet ground, string slice, Dictionary<string, Sprite> sprites, string path,
                                    List<string> frameSlices = null, float framesPerSecond = 0f)
        {
            var template = SideRowTemplate;
            Sprite PieceOf(string from, char row, char side) =>
                sprites.TryGetValue(PieceName(from, WaterfallSides.IndexOf(side), WaterfallRows.IndexOf(row), template), out var p) ? p : null;
            Sprite Piece(char row, char side) => PieceOf(slice, row, side);
            bool animated = frameSlices != null && frameSlices.Count > 1;
            if (Piece('M', 'm') == null)
            {
                Debug.LogWarning($"Bellwort Burrow: skipped {Path.GetFileNameWithoutExtension(path)}; {ground.Sheet} has no {slice} slice.");
                return false;
            }
            var tile = AssetDatabase.LoadAssetAtPath<RuleTile>(path);
            bool isNew = tile == null;
            if (isNew) tile = ScriptableObject.CreateInstance<RuleTile>();
            tile.m_DefaultSprite = Piece('M', 'm');
            tile.m_DefaultColliderType = Tile.ColliderType.None;
            tile.m_TilingRules = new List<RuleTile.TilingRule>();
            var positions = new List<Vector3Int> { new Vector3Int(0, 1, 0), new Vector3Int(0, -1, 0), new Vector3Int(-1, 0, 0), new Vector3Int(1, 0, 0) };
            for (int bits = 0; bits < 16; bits++)
            {
                bool n = (bits & 1) != 0, s = (bits & 2) != 0, w = (bits & 4) != 0, e = (bits & 8) != 0;
                var (row, side) = WaterfallPiece(n, s, w, e);
                var sprite = Piece(row, side) ?? Piece('M', 'm');
                // animated: the same piece from each frame slice in turn (a frame missing a piece shows the first frame's)
                var shown = animated ? frameSlices.Select(f => PieceOf(f, row, side) ?? sprite).ToArray() : new[] { sprite };
                int Is(bool on) => on ? RuleTile.TilingRuleOutput.Neighbor.This : RuleTile.TilingRuleOutput.Neighbor.NotThis;
                tile.m_TilingRules.Add(new RuleTile.TilingRule
                {
                    m_Neighbors = new List<int> { Is(n), Is(s), Is(w), Is(e) },
                    m_NeighborPositions = new List<Vector3Int>(positions),
                    m_Sprites = shown,
                    m_Output = animated ? RuleTile.TilingRuleOutput.OutputSprite.Animation : RuleTile.TilingRuleOutput.OutputSprite.Single,
                    m_MinAnimationSpeed = animated ? framesPerSecond : 1f,
                    m_MaxAnimationSpeed = animated ? framesPerSecond : 1f,
                    m_ColliderType = Tile.ColliderType.None,
                    m_RuleTransform = RuleTile.TilingRuleOutput.Transform.Fixed,
                });
            }
            if (isNew) AssetDatabase.CreateAsset(tile, path);
            EditorUtility.SetDirty(tile);
            return true;
        }

        static bool MakeGrassTile(GroundSheet ground, Dictionary<string, Sprite> sprites, string path)
        {
            var grass = new List<Sprite>();
            foreach (var name in GrassPieces)
                if (sprites.TryGetValue(name, out var sprite)) grass.Add(sprite);
            if (grass.Count == 0) return false;

            var tile = AssetDatabase.LoadAssetAtPath<RuleTile>(path);
            bool isNew = tile == null;
            if (isNew) tile = ScriptableObject.CreateInstance<RuleTile>();

            tile.m_DefaultSprite = grass[0];
            tile.m_DefaultColliderType = Tile.ColliderType.None;
            var rule = new RuleTile.TilingRule
            {
                m_Output = RuleTile.TilingRuleOutput.OutputSprite.Random,
                m_Sprites = ground.EvenPick ? EvenPick(grass, ScatterPerlinScale) : grass.ToArray(),
                m_PerlinScale = ground.EvenPick ? ScatterPerlinScale : 0.5f,
                m_ColliderType = Tile.ColliderType.None,
            };
            rule.m_Neighbors.Clear(); // no conditions: every grass tile picks a random variation
            tile.m_TilingRules = new List<RuleTile.TilingRule> { rule };

            if (isNew) AssetDatabase.CreateAsset(tile, path);
            EditorUtility.SetDirty(tile);
            return true;
        }
    }
}
