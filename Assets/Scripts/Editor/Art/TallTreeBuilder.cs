using System.Collections.Generic;
using BellwortBurrow.Systems.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace BellwortBurrow.EditorTools
{
    /// <summary>
    /// Stacks the tall tree pieces (base, middles, crown) into whole-tree prefabs in Assets/Art/Prefabs/forest_trees_stacked/.
    /// The number in each name is how many middle pieces the trunk has, so vine_tree_2 is a base, two middles and a crown.
    /// Each tree is a nested prefab of the piece prefabs, so redrawing a piece updates every tree.
    /// The root has a Sorting Group at the trunk's foot, so the whole tree, crown included, sorts by where it stands,
    /// and a <see cref="GridFootprint"/> of 2 base tiles. The giant trees also get a <see cref="GiantTree"/> with the
    /// height of every join between trunk pieces, where tree houses go (see <see cref="TreeHouse"/>).
    /// </summary>
    public static class TallTreeBuilder
    {
        public const string Folder = ArtLibrary.PrefabRoot + "/forest_trees_stacked";
        const string PieceFolder = ArtLibrary.PrefabRoot + "/forest_trees";

        sealed class Kind
        {
            public string Name, Base, Middle, KnotMiddle, Crown;
            public int MostMiddles;
            public bool Giant;
            public TreeFit Fit;
        }

        static readonly Kind[] Kinds =
        {
            new Kind { Name = "vine_tree", Base = "vine_base", Middle = "vine_middle", Crown = "vine_crown", MostMiddles = 3 },
            new Kind { Name = "brown_tree", Base = "brown_base", Middle = "brown_middle", KnotMiddle = "brown_middle_knot", Crown = "brown_crown", MostMiddles = 3 },
            new Kind { Name = "wiggle_tree", Base = "wiggle_base", Middle = "wiggle_middle", KnotMiddle = "wiggle_middle_knot", Crown = "wiggle_crown", MostMiddles = 3 },
            new Kind { Name = "stout_tree", Base = "stout_base", Middle = "stout_middle", KnotMiddle = "stout_middle_knot", Crown = "stout_crown", MostMiddles = 3 },
            new Kind { Name = "giant_brown_tree", Base = "giant_brown_base", Middle = "giant_brown_middle", Crown = "giant_brown_crown", MostMiddles = 4, Giant = true },
            new Kind { Name = "giant_wiggle_tree", Base = "giant_wiggle_base", Middle = "giant_wiggle_middle", KnotMiddle = "giant_wiggle_middle_knot", Crown = "giant_wiggle_crown", MostMiddles = 2, Giant = true },
            new Kind { Name = "giant_stout_tree", Base = "giant_stout_base", Middle = "giant_stout_middle", KnotMiddle = "giant_stout_middle_knot", Crown = "giant_stout_crown", MostMiddles = 3, Giant = true },
            new Kind { Name = "giant_vine_tree", Base = "giant_vine_base", Middle = "giant_vine_middle", Crown = "giant_vine_crown", MostMiddles = 2, Giant = true, Fit = TreeFit.Vine },
        };

        [MenuItem("Bellwort Burrow/Setup/Forest Steps/3. Stack Tall Trees", false, 203)]
        static void MakeAllFromMenu()
        {
            if (!ArtLibrary.CheckArtInstalled()) return;
            Debug.Log($"Bellwort Burrow: stacked {MakeAll()} tall trees in {Folder}.");
        }

        /// <summary>Builds every tall tree prefab. Needs the forest_trees piece prefabs first. Trees changed by hand are left alone.</summary>
        public static int MakeAll()
        {
            ArtLibrary.EnsureFolder(Folder);
            int made = 0;
            var kept = new List<string>();
            foreach (var kind in Kinds)
            {
                var basePiece = LoadPiece(kind.Base);
                var middle = LoadPiece(kind.Middle);
                var knot = kind.KnotMiddle != null ? LoadPiece(kind.KnotMiddle) : null;
                var crown = LoadPiece(kind.Crown);
                if (basePiece == null || middle == null || crown == null)
                {
                    Debug.LogWarning($"Bellwort Burrow: skipped {kind.Name}; make the forest_trees prefabs first.");
                    continue;
                }

                for (int middles = 0; middles <= kind.MostMiddles; middles++)
                {
                    string prefabPath = $"{Folder}/{kind.Name}_{middles}.prefab";
                    if (!GeneratedAssets.CanOverwrite(prefabPath)) { kept.Add(prefabPath); continue; }

                    var root = new GameObject($"{kind.Name}_{middles}");
                    root.AddComponent<SortingGroup>();
                    int order = 0;
                    var joins = new List<float>();
                    float top = AddPiece(root, basePiece, 0f, order++);
                    joins.Add(top);
                    for (int i = 0; i < middles; i++)
                    {
                        // Trunks with a knot show it second from the bottom (or alone on a one-middle tree).
                        bool useKnot = knot != null && (middles == 1 || i % 2 == 1);
                        top = AddPiece(root, useKnot ? knot : middle, top, order++);
                        joins.Add(top);
                    }
                    AddPiece(root, crown, top, order);
                    root.AddComponent<GridFootprint>().tilesWide = 2;
                    if (kind.Giant)
                    {
                        var giant = root.AddComponent<GiantTree>();
                        giant.fit = kind.Fit;
                        giant.joins = joins.ToArray();
                    }

                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                    Object.DestroyImmediate(root);
                    GeneratedAssets.Stamp(prefabPath);
                    made++;
                }
            }
            GeneratedAssets.ReportKept("Stack Tall Trees", kept);
            return made;
        }

        static GameObject LoadPiece(string name) =>
            AssetDatabase.LoadAssetAtPath<GameObject>($"{PieceFolder}/{name}.prefab");

        /// <summary>Puts a piece with its feet at height y and returns the height of its top.</summary>
        static float AddPiece(GameObject root, GameObject piecePrefab, float y, int order)
        {
            var piece = (GameObject)PrefabUtility.InstantiatePrefab(piecePrefab, root.transform);
            // Unity matches children by name when this tree is rebuilt, so repeated pieces get their own names
            // ("vine_middle", "vine_middle 2") and stay the same objects across rebuilds.
            int repeats = 0;
            foreach (Transform sibling in root.transform)
                if (sibling != piece.transform && PrefabUtility.GetCorrespondingObjectFromSource(sibling.gameObject) == piecePrefab) repeats++;
            if (repeats > 0) piece.name = $"{piecePrefab.name} {repeats + 1}";
            piece.transform.localPosition = new Vector3(0f, y, 0f);

            var renderer = piece.GetComponent<SpriteRenderer>();
            if (piece.TryGetComponent<SortingGroup>(out var group)) group.sortingOrder = order;
            else if (renderer != null) renderer.sortingOrder = order;

            if (renderer == null || renderer.sprite == null) return y;
            // Measure from the pivot (the feet), not the bottom of the slice: a base can draw a little below its feet,
            // like the contact shadow under the tree homes.
            return y + (renderer.sprite.rect.height - renderer.sprite.pivot.y) / renderer.sprite.pixelsPerUnit;
        }
    }
}
