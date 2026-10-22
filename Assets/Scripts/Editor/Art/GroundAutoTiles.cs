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
    /// forest_ground makes the Forest tiles, burrow_ground the Burrow tiles). Every ground sheet has the same slices, and
    /// each makes:
    /// - Path and Deer Trail (Forest Path, Forest Deer Trail): Rule Tiles that read all eight neighbours. Paint where the
    ///   path goes, any width down to one tile, and each tile picks its piece from which neighbours are painted. The
    ///   edge sits a couple of pixels inside the painted tiles, so every tile you paint shows. Paint a staircase and the
    ///   steps become a slope: a corner whose next step carries on diagonally is cut from tile corner to tile corner.
    /// - Path Half and Deer Trail Half: a half tile of each. Set it beside the full tile and it fills the half of its
    ///   tile that touches it (beside two, both halves), so a path or trail can end half way into a tile, be half a tile
    ///   wider, or fill the inside of a turn. Each full and half pair are Path Rule Tiles (<see cref="PathRuleTile"/>):
    ///   Rule Tiles whose neighbour conditions tell full, half and neither apart.
    /// - Grass (Forest Grass): a Rule Tile that picks one of the grass variations at random.
    /// A ground sheet with stone (<see cref="GroundSheet.Stone"/>: burrow_ground) also makes Flagstones and Flagstones
    /// Half (Burrow Flagstones), a full and a half pair like the path's, with the paths' grass fringe for an edge.
    /// A ground sheet with water (<see cref="GroundSheet.Water"/>: burrow_ground) also makes Water and Water Half
    /// (Burrow Water), a full and a half pair like the path's: the water with its pebbled banks, the far one a wall.
    /// With raised ground too, it makes the Waterfall (Burrow Waterfall), painted over a bank face where the creek runs
    /// off the edge (<see cref="WaterfallPiece"/>).
    /// A ground sheet with raised ground (<see cref="GroundSheet.Banks"/>: burrow_ground) also makes:
    /// - Bank Face and Cliff Face (Burrow Bank Face, Burrow Cliff Face): the earth or stone face under raised ground's
    ///   front edge (the raised ground itself is plain Grass), painted three rows tall (four in a 45 degree column). They are Face Tiles (<see cref="FaceTile"/>), which
    ///   read their own column and their neighbours' to draw the lip and foot, step borders, 45 degree runs, inner
    ///   corner creases and open ends, each look in several variations (<see cref="FacePieceKeys"/>).
    /// These are ordinary Unity tile assets. Open one in the Inspector to see or change its rules. Running this again
    /// refreshes them in place, so scenes painted with them keep working; a tile you changed by hand is left alone
    /// (see <see cref="GeneratedAssets"/>).
    ///
    /// Every ground set is painted together on the sheet as one patch, from <see cref="Template"/>: each painted ('#')
    /// cell of the slice is a piece, and its layout is read from its neighbours in the template, exactly as the Rule
    /// Tile reads them in a scene. The template holds all 83 layouts (<see cref="Layout"/>); a layout that comes up
    /// more than once gives the tile several pieces to pick from at random. The half sets are painted the same way
    /// from <see cref="HalfTemplate"/>. tools/ground_sets.py reads these templates too.
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
            /// <summary>Whether the sheet has flagstones laid like a path (<see cref="StonePairs"/>: burrow_ground).</summary>
            public bool Stone;
            /// <summary>Whether the sheet has water laid like a path (<see cref="WaterPairs"/>: burrow_ground).</summary>
            public bool Water;

            /// <summary>Where one of its tiles is saved, by kind ("Grass", "Path", "Deer Trail Half").</summary>
            public string Tile(string kind) => $"{Folder}/{Zone} {kind}.asset";
        }

        /// <summary>Every zone's ground sheet, in the order setup makes them.</summary>
        public static readonly GroundSheet[] Sheets =
        {
            new GroundSheet { Sheet = "forest_ground", Zone = "Forest", Palette = "Forest Ground" },
            new GroundSheet { Sheet = "burrow_ground", Zone = "Burrow", Palette = "Burrow Ground", Banks = true, Stone = true, Water = true },
        };

        /// <summary>A ground that comes as a full tile and a half tile: its set, its half set and its extra full pieces.</summary>
        sealed class HalfPair
        {
            public string Kind, Slice, HalfSlice;
            public string[] Fills; // extra full pieces, picked at random alongside the template's full pieces
            /// <summary>The half tiles that cut 45 degrees, painted from <see cref="HalfSlopeTemplate"/>.</summary>
            public string SlopeSlice => SlopeSliceOf(HalfSlice);
        }

        /// <summary>A half set's slope set: path_half_set has path_slope_set.</summary>
        internal static string SlopeSliceOf(string halfSlice) => halfSlice.Replace("_half_set", "_slope_set");

        /// <summary>The grounds every ground sheet has, in the order the tile palette lays them out.</summary>
        static readonly HalfPair[] Pairs =
        {
            new HalfPair { Kind = "Path", Slice = "path_set", HalfSlice = "path_half_set", Fills = new[] { "path_fill_00", "path_fill_01" } },
            new HalfPair { Kind = "Deer Trail", Slice = "trail_set", HalfSlice = "trail_half_set", Fills = new[] { "trail_00", "trail_01" } },
        };

        /// <summary>
        /// A sheet with stone (<see cref="GroundSheet.Stone"/>) also lays flagstones as a path is laid: Flagstones and
        /// Flagstones Half, from flag_set and flag_half_set, with the stone_flag tiles as extra full pieces.
        /// </summary>
        static readonly HalfPair[] StonePairs =
        {
            new HalfPair { Kind = "Flagstones", Slice = "flag_set", HalfSlice = "flag_half_set",
                           Fills = new[] { "stone_flag_00", "stone_flag_01", "stone_flag_02", "stone_flag_03" } },
        };

        /// <summary>
        /// A sheet with water (<see cref="GroundSheet.Water"/>) also lays water as a path is laid: Water and Water Half,
        /// from water_set and water_half_set, so a creek or a river of any width bends half a tile at a time and cuts 45
        /// degrees on a staircase. The far bank's wall and the stones along the waterline are drawn in each piece.
        /// </summary>
        static readonly HalfPair[] WaterPairs =
        {
            new HalfPair { Kind = "Water", Slice = "water_set", HalfSlice = "water_half_set", Fills = new string[0] },
        };

        /// <summary>The full and half grounds a sheet has, in the order its tile palette lays them out.</summary>
        static IEnumerable<HalfPair> PairsOf(GroundSheet ground)
        {
            foreach (var pair in Pairs) yield return pair;
            if (ground.Stone) foreach (var pair in StonePairs) yield return pair;
            if (ground.Water) foreach (var pair in WaterPairs) yield return pair;
        }

        /// <summary>Raised ground's faces: each kind and its set of pieces (<see cref="FacePieceKeys"/>).</summary>
        internal static readonly (string kind, string slice)[] Faces =
        {
            ("Bank Face", "bank_face_set"),
            ("Cliff Face", "cliff_face_set"),
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

        /// <summary>
        /// The patch the half sets (half path, half deer trail) are painted from, top row first: 'H' is a half tile,
        /// 'F' a full tile and '.' neither. Every look a half tile can have (<see cref="HalfLook"/>) comes up on at least
        /// one 'H'; the 'F' tiles are there so the halves have a full tile to turn toward, and become more full pieces.
        /// Change it only together with the sheets' slices.
        /// </summary>
        public static readonly string[] HalfTemplate =
        {
            "HF.F.FFHFH.FHHHFH",
            "FHFHFHHHHHFHFHHFH",
            ".FHF.F.FFFFHHFFFH",
            ".F.FFHFHHHFFHFHHF",
            "FHFHHHFHFHFHHFFHF",
            ".FFHFF.HFH.FFF.FH",
            ".FFHHHFF.F.FFHFHH",
            "FHHF.FHHHHFHFHHFF",
            "FHFHFFHFFF.F.FFH.",
            ".F.F.HF..FHHFHFHF",
            ".HFHFFHFFHFFHF.FH",
            "FHF.F.HFHHFFHHHFH",
            ".F.FHFHFF.HHFFHF.",
            "FFF.F.FHHF.F.FF.F",
            "HHH.FF.FHFFHFHHFH",
            "FHFFHHFHHHHFH.F.F",
            "FHHFF..FFFH.FFHF.",
            "HFF.HHFHFFHFHFHHF",
            "F.FHFF.FHF.F.FHFF",
            "HF.FFFHHHFFH.HFHH",
            "F.FH..FFHFFHF.H..",
            "FHFFHF.FHF.FHFHF.",
            ".HF.F.H.F.FHF.FHF",
            "HFHHFHFH.HFFHFH.H",
            ".FFFHF.FHF.FHF..F",
            "....FF.FHF.FF....",
            ".......FFF..HF...",
        };

        /// <summary>
        /// The patch the half sets' slope sets (path_slope_set beside path_half_set, and so on) are painted from, laid out
        /// as <see cref="HalfTemplate"/>: every half look <see cref="HalfTemplate"/> doesn't hold comes up on at least one
        /// 'H' here. These are the looks that cut 45 degrees (<see cref="HalfLook"/>). A sheet without a slope set still
        /// works: those half tiles take the rounded look they had before (<see cref="RoundedLook"/>).
        /// Change it only together with the sheets' slices.
        /// </summary>
        public static readonly string[] HalfSlopeTemplate =
        {
            "FHH..FFFF..HFHF..",
            ".FFHFH.HHF.F.HFFF",
            "FHHHFHH..HFH.FFHH",
            "...F.FHFHHF.F...H",
            "FHHFHHF.F.F.FFF.H",
            "FFF.FHFFH.HFHHFHH",
            ".HFHHFF.HFH.H.HF.",
            "H..F..HFF.FH.HFH.",
            "HFHHF.HHFFH..F..H",
            ".FF.HHFFH..F..HFF",
            ".HHF.FH.H.HF.FFF.",
            "FHHFHH.F.FFFH.HF.",
            "FHH...HH...FFFHH.",
            ".H.HF.HH......HF.",
            ".FH....F.....H...",
        };

        /// <summary>How many looks a half tile has.</summary>
        public const int HalfLookCount = 191;

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

        // Perlin noise favors the middle of the list, so the plainest grass goes in the middle.
        static readonly string[] GrassPieces = { "grass_00", "grass_01", "grass_02", "grass_03" };

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
            foreach (var pair in PairsOf(ground))
            {
                string fullPath = ground.Tile(pair.Kind), halfPath = ground.Tile(pair.Kind + " Half");
                var locked = new[] { fullPath, halfPath }.Where(p => !GeneratedAssets.CanOverwrite(p)).ToList();
                if (locked.Count > 0) kept.AddRange(locked);
                else if (MakeHalfPair(ground, pair, sprites, fullPath, halfPath)) written.AddRange(new[] { fullPath, halfPath });
            }
            string grassPath = ground.Tile("Grass");
            if (!GeneratedAssets.CanOverwrite(grassPath)) kept.Add(grassPath);
            else if (MakeGrassTile(sprites, grassPath)) written.Add(grassPath);
            if (ground.Banks)
            {
                foreach (var (kind, slice) in Faces)
                {
                    string facePath = ground.Tile(kind);
                    if (!GeneratedAssets.CanOverwrite(facePath)) kept.Add(facePath);
                    else if (MakeFaceTile(ground, slice, sprites, facePath)) written.Add(facePath);
                }
                if (ground.Water)
                {
                    string fallPath = ground.Tile("Waterfall");
                    if (!GeneratedAssets.CanOverwrite(fallPath)) kept.Add(fallPath);
                    else if (MakeWaterfallTile(ground, sprites, fallPath)) written.Add(fallPath);
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
            foreach (var pair in PairsOf(ground))
            {
                yield return ground.Tile(pair.Kind);
                yield return ground.Tile(pair.Kind + " Half");
            }
            if (!ground.Banks) yield break;
            foreach (var (kind, _) in Faces) yield return ground.Tile(kind);
            if (ground.Water) yield return ground.Tile("Waterfall");
        }

        /// <summary>
        /// The ground sets painted from <see cref="Template"/>, in the order the tile palette lays them out: each one's
        /// slice, its half set's slice (painted from <see cref="HalfTemplate"/>) and the extra full pieces it picks from
        /// (see <see cref="GroundPalette"/>). Every ground sheet has the path and deer trail; a sheet with stone has the
        /// flagstones too.
        /// </summary>
        internal static IEnumerable<(string slice, string halfSlice, string[] fills)> TemplateSets(GroundSheet ground)
        {
            foreach (var pair in PairsOf(ground)) yield return (pair.Slice, pair.HalfSlice, pair.Fills);
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

        // ---- Full and half path ----------------------------------------------------------------------------------
        // A neighbourhood is the eight neighbours' types in the Rule Tile's order (NW, N, NE, W, E, SW, S, SE):
        // 'F' full path, 'H' half path, '.' neither. Quarters of a tile, top row first: top left 1, top right 2,
        // bottom left 4, bottom right 8.
        const int TL = 1, TR = 2, BL = 4, BR = 8;
        const string Types = "FH.";

        /// <summary>How many neighbourhoods there are: three types for each of eight neighbours.</summary>
        internal const int Neighbourhoods = 6561;

        /// <summary>
        /// Which quarters of its tile a tile fills, from its type and its side neighbours' types. A full tile fills
        /// all four. A half tile turns toward the full path beside it: the two quarters on each side with a full tile.
        /// With no full tile beside it, a half tile is a lone dot (no quarters).
        /// </summary>
        internal static int Quarters(char tile, char n, char s, char w, char e)
        {
            if (tile == 'F') return TL | TR | BL | BR;
            if (tile != 'H') return 0;
            int q = 0;
            if (n == 'F') q |= TL | TR;
            if (s == 'F') q |= BL | BR;
            if (w == 'F') q |= TL | BL;
            if (e == 'F') q |= TR | BR;
            return q;
        }

        /// <summary>The neighbourhood with the given number (0 to 6560): each neighbour's type is one base 3 digit.</summary>
        internal static char[] NeighbourhoodOf(int number)
        {
            var around = new char[8];
            for (int i = 0; i < 8; i++) { around[i] = Types[number % 3]; number /= 3; }
            return around;
        }

        /// <summary>A template cell's neighbourhood ('#' counts as full path; outside the template is neither).</summary>
        internal static char[] Around(string[] template, int column, int row)
        {
            char At(int dx, int dy)
            {
                int x = column + dx, y = row + dy;
                if (y < 0 || y >= template.Length || x < 0 || x >= template[y].Length) return '.';
                char c = template[y][x];
                return c == '#' ? 'F' : c;
            }
            return new[] { At(-1, -1), At(0, -1), At(1, -1), At(-1, 0), At(1, 0), At(-1, 1), At(0, 1), At(1, 1) };
        }

        /// <summary>
        /// Path quarters of a tile and its eight neighbours, as a 6 by 6 grid (row 0 at the top; the tile's own
        /// quarters at rows and columns 2 and 3). Tiles past the neighbours count as neither, which only changes
        /// quarters away from the tile.
        /// </summary>
        static bool[,] QuarterGrid(char tile, char[] around)
        {
            var t = new char[5, 5];
            for (int y = 0; y < 5; y++) for (int x = 0; x < 5; x++) t[y, x] = '.';
            t[1, 1] = around[0]; t[1, 2] = around[1]; t[1, 3] = around[2];
            t[2, 1] = around[3]; t[2, 2] = tile; t[2, 3] = around[4];
            t[3, 1] = around[5]; t[3, 2] = around[6]; t[3, 3] = around[7];
            var q = new bool[6, 6];
            for (int y = 0; y < 3; y++)
            {
                for (int x = 0; x < 3; x++)
                {
                    int cy = y + 1, cx = x + 1;
                    int bits = Quarters(t[cy, cx], t[cy - 1, cx], t[cy + 1, cx], t[cy, cx - 1], t[cy, cx + 1]);
                    q[2 * y, 2 * x] = (bits & TL) != 0; q[2 * y, 2 * x + 1] = (bits & TR) != 0;
                    q[2 * y + 1, 2 * x] = (bits & BL) != 0; q[2 * y + 1, 2 * x + 1] = (bits & BR) != 0;
                }
            }
            return q;
        }

        /// <summary>
        /// The layout (bits as for <see cref="LayoutOf"/>) of a full path tile among full and half path. A half tile
        /// beside it always turns toward it, so a side with either kind is painted. An inner corner is closed when the
        /// quarter in that corner is path. A slope carries on only through full tiles. Among full tiles alone this is
        /// the same as <see cref="LayoutOf"/>.
        /// </summary>
        internal static uint FullLayout(char[] around)
        {
            bool Path(char c) => c == 'F' || c == 'H';
            char nw = around[0], n = around[1], ne = around[2], w = around[3], e = around[4], sw = around[5], s = around[6], se = around[7];
            bool pn = Path(n), ps = Path(s), pw = Path(w), pe = Path(e);
            uint layout = 16;
            if (pn) layout |= N;
            if (ps) layout |= S;
            if (pw) layout |= W;
            if (pe) layout |= E;
            uint relevant = RelevantCorners(pn, ps, pw, pe);
            var q = QuarterGrid('F', around);
            (uint bit, char diagonal, char vertical, char horizontal, bool a, bool b, bool cornerQuarter)[] corners =
            {
                (NW, nw, n, w, pn, pw, q[1, 1]), (NE, ne, n, e, pn, pe, q[1, 4]),
                (SW, sw, s, w, ps, pw, q[4, 1]), (SE, se, s, e, ps, pe, q[4, 4]),
            };
            foreach (var c in corners)
            {
                if ((relevant & c.bit) == 0) continue;
                bool on;
                if (c.a && c.b) on = c.cornerQuarter;
                else on = c.diagonal == 'F' && (c.a ? c.vertical : c.horizontal) == 'F';
                if (on) layout |= c.bit;
            }
            return layout;
        }

        /// <summary>
        /// What a half path tile's piece looks like, as a key: the quarters it fills and, around each of them, the
        /// quarters that can change it (the four beside it, and a diagonal one only where both quarters between are
        /// path, as that closes an inner corner), then after a '|' the outer corners it cuts on a slope, if any.
        /// "lone" when it fills none. <see cref="HalfLookCount"/> looks in all.
        /// </summary>
        internal static string HalfLook(char[] around)
        {
            var q = QuarterGrid('H', around);
            bool P(int qy, int qx) => q[2 + qy, 2 + qx];
            int own = 0;
            var mine = new List<(int y, int x)>();
            for (int qy = 0; qy < 2; qy++)
                for (int qx = 0; qx < 2; qx++)
                    if (P(qy, qx)) { own |= 1 << (qy * 2 + qx); mine.Add((qy, qx)); }
            if (own == 0) return "lone";
            var seen = new SortedDictionary<int, bool>();
            foreach (var (y, x) in mine)
            {
                foreach (var (dy, dx) in new[] { (-1, 0), (1, 0), (0, -1), (0, 1) })
                    seen[(y + dy + 1) * 4 + (x + dx + 1)] = P(y + dy, x + dx);
                foreach (var (dy, dx) in new[] { (-1, -1), (-1, 1), (1, -1), (1, 1) })
                    if (P(y + dy, x) && P(y, x + dx)) seen[(y + dy + 1) * 4 + (x + dx + 1)] = P(y + dy, x + dx);
            }
            string look = own + ":" + string.Join(",", seen.Select(kv => kv.Key + (kv.Value ? "+" : "-")));
            // An outer corner of a quarter (both quarters beside it there aren't path) is cut on a slope, as a full
            // tile's is, where the quarter staircase carries on past an end of it: past the far end of its edge along
            // the top or bottom (the quarter beside it there and the one past that are path), or past the far end of
            // its edge along the side.
            var slopes = new List<string>();
            foreach (var (y, x) in mine)
                foreach (var (sy, sx) in new[] { (1, -1), (1, 1), (-1, -1), (-1, 1) })
                {
                    if (P(y, x - sx) || P(y - sy, x)) continue;
                    bool alongTop = P(y, x + sx) && P(y - sy, x + sx);
                    bool alongSide = P(y + sy, x) && P(y + sy, x - sx);
                    if (alongTop || alongSide) slopes.Add($"{y}{x}{(sy > 0 ? 'n' : 's')}{(sx > 0 ? 'w' : 'e')}{(alongTop ? 1 : 0)}{(alongSide ? 1 : 0)}");
                }
            return slopes.Count == 0 ? look : look + "|" + string.Join(",", slopes);
        }

        /// <summary>
        /// A half look as it is without its slopes: the rounded look a half tile had before half tiles cut 45 degrees,
        /// which it takes on a sheet with no piece for its look.
        /// </summary>
        internal static string RoundedLook(string look)
        {
            int bar = look.IndexOf('|');
            return bar < 0 ? look : look.Substring(0, bar);
        }

        // Neighbour conditions of a Path Rule Tile, and the types each one lets through.
        static readonly (int code, string types)[] Conditions =
        {
            (0, "FH."), (PathRuleTile.Neighbor.AnyPath, "FH"), (PathRuleTile.Neighbor.NotFull, "H."), (PathRuleTile.Neighbor.NotHalf, "F."),
            (PathRuleTile.Neighbor.Full, "F"), (PathRuleTile.Neighbor.Half, "H"), (PathRuleTile.Neighbor.NoPath, "."),
        };

        internal static string TypesOf(int condition) => Conditions.First(c => c.code == condition).types;

        /// <summary>
        /// Rule Tile rules for a tile whose piece is keyOf(neighbourhood): for each key, neighbour conditions that
        /// together let through exactly the neighbourhoods with that key. A neighbourhood may match more than one rule,
        /// but only rules for its own piece, so the order of the rules doesn't matter.
        /// </summary>
        internal static List<(T key, int[] conditions)> PathRules<T>(Func<char[], T> keyOf)
        {
            var keys = new T[Neighbourhoods];
            for (int i = 0; i < Neighbourhoods; i++) keys[i] = keyOf(NeighbourhoodOf(i));
            var covered = new bool[Neighbourhoods];
            var rules = new List<(T, int[])>();
            int[] pow = { 1, 3, 9, 27, 81, 243, 729, 2187 };

            bool AllSame(string[] types, T key)
            {
                // every neighbourhood the conditions let through has this key
                var digits = new int[8];
                while (true)
                {
                    int number = 0;
                    for (int i = 0; i < 8; i++) number += Types.IndexOf(types[i][digits[i]]) * pow[i];
                    if (!EqualityComparer<T>.Default.Equals(keys[number], key)) return false;
                    int d = 0;
                    while (d < 8 && ++digits[d] == types[d].Length) { digits[d] = 0; d++; }
                    if (d == 8) return true;
                }
            }

            for (int start = 0; start < Neighbourhoods; start++)
            {
                if (covered[start]) continue;
                T key = keys[start];
                var around = NeighbourhoodOf(start);
                var types = around.Select(c => c.ToString()).ToArray();
                for (int i = 0; i < 8; i++)
                {
                    foreach (var (_, wider) in Conditions)
                    {
                        if (wider.Length <= types[i].Length || wider.IndexOf(around[i]) < 0) continue;
                        var trial = (string[])types.Clone();
                        trial[i] = wider;
                        if (AllSame(trial, key)) { types = trial; break; }
                    }
                }
                var conditions = types.Select(t => Conditions.First(c => c.types == t).code).ToArray();
                rules.Add((key, conditions));
                // mark what these conditions let through
                var digits = new int[8];
                while (true)
                {
                    int number = 0;
                    for (int i = 0; i < 8; i++) number += Types.IndexOf(types[i][digits[i]]) * pow[i];
                    covered[number] = true;
                    int d = 0;
                    while (d < 8 && ++digits[d] == types[d].Length) { digits[d] = 0; d++; }
                    if (d == 8) break;
                }
            }
            return rules;
        }

        // The rules are the same for every pair, so they're worked out once.
        static List<(uint key, int[] conditions)> fullRules;
        static List<(string key, int[] conditions)> halfRules;

        static RuleTile.TilingRule PathRule(int[] conditions, List<Sprite> pieces) => new RuleTile.TilingRule
        {
            m_Neighbors = new List<int>(conditions),
            m_NeighborPositions = new List<Vector3Int>(RulePositions),
            m_Sprites = pieces.ToArray(),
            m_Output = pieces.Count > 1 ? RuleTile.TilingRuleOutput.OutputSprite.Random : RuleTile.TilingRuleOutput.OutputSprite.Single,
            m_PerlinScale = 0.5f,
            m_ColliderType = Tile.ColliderType.None,
            m_RuleTransform = RuleTile.TilingRuleOutput.Transform.Fixed,
        };

        /// <summary>
        /// Makes a full and half pair, like Forest Path and Forest Path Half: the full tile from its set (and the full
        /// tiles of its half set), the half tile from its half set.
        /// </summary>
        static bool MakeHalfPair(GroundSheet ground, HalfPair pair, Dictionary<string, Sprite> sprites, string pathPath, string halfPath)
        {
            string fullName = Path.GetFileNameWithoutExtension(pathPath), halfName = Path.GetFileNameWithoutExtension(halfPath);
            var full = new Dictionary<uint, List<Sprite>>();
            var half = new Dictionary<string, List<Sprite>>();
            int missing = 0;
            void AddFull(uint layout, Sprite piece) { if (!full.TryGetValue(layout, out var l)) full[layout] = l = new List<Sprite>(); l.Add(piece); }
            for (int row = 0; row < Template.Length; row++)
                for (int column = 0; column < Template[row].Length; column++)
                {
                    if (Template[row][column] != '#') continue;
                    if (sprites.TryGetValue(PieceName(pair.Slice, column, row, Template), out var piece)) AddFull(Layout(Template, column, row), piece);
                    else missing++;
                }
            int slopePieces = 0, slopeMissing = 0;
            foreach (var (template, slice) in new[] { (HalfTemplate, pair.HalfSlice), (HalfSlopeTemplate, pair.SlopeSlice) })
                for (int row = 0; row < template.Length; row++)
                    for (int column = 0; column < template[row].Length; column++)
                    {
                        char type = template[row][column];
                        if (type == '.') continue;
                        bool slopes = template == HalfSlopeTemplate;
                        if (!sprites.TryGetValue(PieceName(slice, column, row, template), out var piece))
                        {
                            if (slopes) slopeMissing++; else missing++;
                            continue;
                        }
                        if (slopes) slopePieces++;
                        var around = Around(template, column, row);
                        if (type == 'F') AddFull(FullLayout(around), piece);
                        else
                        {
                            string look = HalfLook(around);
                            if (!half.TryGetValue(look, out var l)) half[look] = l = new List<Sprite>();
                            l.Add(piece);
                        }
                    }
            if (slopePieces > 0) missing += slopeMissing;
            if (!full.ContainsKey(Full))
            {
                Debug.LogWarning($"Bellwort Burrow: skipped {fullName}; {ground.Sheet} has no {pair.Slice} slice painted from the template.");
                return false;
            }
            foreach (var name in pair.Fills)
                if (sprites.TryGetValue(name, out var fill)) full[Full].Add(fill);
            if (missing > 0)
                Debug.LogWarning($"Bellwort Burrow: {fullName}: {missing} tiles of the templates have no piece in {pair.Slice} or {pair.HalfSlice}. " +
                                 "Are the slices the size of GroundAutoTiles.Template and HalfTemplate, and painted from them?");

            var pathTile = ScriptableObject.CreateInstance<PathRuleTile>();
            pathTile.name = fullName;
            pathTile.m_DefaultSprite = full[Full][0];
            pathTile.m_DefaultColliderType = Tile.ColliderType.None;
            pathTile.m_TilingRules = new List<RuleTile.TilingRule>();
            foreach (var (layout, conditions) in fullRules ??= PathRules<uint>(FullLayout))
                if (full.TryGetValue(layout, out var pieces)) pathTile.m_TilingRules.Add(PathRule(conditions, pieces));
            if (full.Count < LayoutCount)
                Debug.LogWarning($"Bellwort Burrow: {fullName} has {full.Count} of the {LayoutCount} layouts; tiles with a missing layout show the full piece.");

            var halfTile = ScriptableObject.CreateInstance<PathRuleTile>();
            halfTile.name = halfName;
            var facingWest = new[] { '.', '.', '.', 'F', '.', '.', '.', '.' };
            halfTile.m_DefaultSprite = half.TryGetValue(HalfLook(facingWest), out var west) ? west[0] : full[Full][0];
            halfTile.m_DefaultColliderType = Tile.ColliderType.None;
            halfTile.m_TilingRules = new List<RuleTile.TilingRule>();
            // A look the sheet has no piece for (a sheet painted before half tiles cut 45 degrees) takes the pieces of
            // its rounded look, or of any look with the same rounded look: on such a sheet they're all drawn rounded.
            var byRounded = new Dictionary<string, List<Sprite>>();
            foreach (var kv in half)
                if (!byRounded.ContainsKey(RoundedLook(kv.Key)) || kv.Key == RoundedLook(kv.Key)) byRounded[RoundedLook(kv.Key)] = kv.Value;
            foreach (var (look, conditions) in halfRules ??= PathRules<string>(HalfLook))
            {
                if (!half.TryGetValue(look, out var pieces) && !byRounded.TryGetValue(RoundedLook(look), out pieces)) continue;
                halfTile.m_TilingRules.Add(PathRule(conditions, pieces));
            }
            if (slopePieces == 0)
                Debug.Log($"Bellwort Burrow: {ground.Sheet} has no {pair.SlopeSlice}, so {halfName} keeps its rounded corners where it would cut 45 degrees.");
            else if (half.Count < HalfLookCount)
                Debug.LogWarning($"Bellwort Burrow: {halfName} has {half.Count} of the {HalfLookCount} looks; half tiles with a missing look show its first piece.");

            SaveKeepingId(pathTile, pathPath);
            SaveKeepingId(halfTile, halfPath);
            // Each names the other, so their rules can tell full and half path apart.
            var savedPath = AssetDatabase.LoadAssetAtPath<PathRuleTile>(pathPath);
            var savedHalf = AssetDatabase.LoadAssetAtPath<PathRuleTile>(halfPath);
            if (savedPath == null || savedHalf == null)
            {
                Debug.LogWarning($"Bellwort Burrow: couldn't load {pathPath} and {halfPath} back as Path Rule Tiles; run Make Ground Auto Tiles again after scripts reload.");
                return true;
            }
            foreach (var tile in new[] { savedPath, savedHalf })
            {
                tile.fullPath = savedPath;
                tile.halfPath = savedHalf;
                EditorUtility.SetDirty(tile);
            }
            return true;
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
        static bool MakeFaceTile(GroundSheet ground, string slice, Dictionary<string, Sprite> sprites, string path)
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

            var tile = ScriptableObject.CreateInstance<FaceTile>();
            tile.name = name;
            tile.keys = keys.ToArray();
            tile.sprites = pieces.ToArray();
            int plain = keys.IndexOf("FMcc");
            tile.defaultSprite = plain >= 0 ? pieces[plain] : pieces[0];
            SaveKeepingId(tile, path);
            return true;
        }

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
        static bool MakeWaterfallTile(GroundSheet ground, Dictionary<string, Sprite> sprites, string path)
        {
            var template = new[] { new string('#', WaterfallSides.Length), new string('#', WaterfallSides.Length), new string('#', WaterfallSides.Length) };
            Sprite Piece(char row, char side) =>
                sprites.TryGetValue(PieceName(WaterfallSlice, WaterfallSides.IndexOf(side), WaterfallRows.IndexOf(row), template), out var p) ? p : null;
            if (Piece('M', 'm') == null)
            {
                Debug.LogWarning($"Bellwort Burrow: skipped {Path.GetFileNameWithoutExtension(path)}; {ground.Sheet} has no {WaterfallSlice} slice.");
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
                int Is(bool on) => on ? RuleTile.TilingRuleOutput.Neighbor.This : RuleTile.TilingRuleOutput.Neighbor.NotThis;
                tile.m_TilingRules.Add(new RuleTile.TilingRule
                {
                    m_Neighbors = new List<int> { Is(n), Is(s), Is(w), Is(e) },
                    m_NeighborPositions = new List<Vector3Int>(positions),
                    m_Sprites = new[] { sprite },
                    m_Output = RuleTile.TilingRuleOutput.OutputSprite.Single,
                    m_ColliderType = Tile.ColliderType.None,
                    m_RuleTransform = RuleTile.TilingRuleOutput.Transform.Fixed,
                });
            }
            if (isNew) AssetDatabase.CreateAsset(tile, path);
            EditorUtility.SetDirty(tile);
            return true;
        }

        static bool MakeGrassTile(Dictionary<string, Sprite> sprites, string path)
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
                m_Sprites = grass.ToArray(),
                m_PerlinScale = 0.5f,
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
