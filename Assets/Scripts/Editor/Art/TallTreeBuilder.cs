using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace BellwortBurrow.EditorTools
{
    /// <summary>
    /// Stacks the tall tree pieces (base, middles, crown) into whole-tree prefabs in Assets/Art/Prefabs/forest_trees_stacked/.
    /// The number in each name is how many middle pieces the trunk has, so vine_tree_2 is a base, two middles and a crown.
    /// Each tree is a nested prefab of the piece prefabs, so redrawing a piece updates every tree.
    /// The root has a Sorting Group at the trunk's foot, so the whole tree, crown included, sorts by where it stands.
    /// </summary>
    public static class TallTreeBuilder
    {
        public const string Folder = ArtLibrary.PrefabRoot + "/forest_trees_stacked";
        const string PieceFolder = ArtLibrary.PrefabRoot + "/forest_trees";

        sealed class Kind
        {
            public string Name, Base, Middle, KnotMiddle, Crown;
            public int MostMiddles;
        }

        static readonly Kind[] Kinds =
        {
            new Kind { Name = "vine_tree", Base = "vine_base", Middle = "vine_middle", Crown = "vine_crown", MostMiddles = 3 },
            new Kind { Name = "brown_tree", Base = "brown_base", Middle = "brown_middle", KnotMiddle = "brown_middle_knot", Crown = "brown_crown", MostMiddles = 3 },
            new Kind { Name = "giant_vine_home", Base = "giant_vine_home_base", Middle = "giant_vine_middle", Crown = "giant_vine_crown", MostMiddles = 2 },
            new Kind { Name = "giant_brown_home", Base = "giant_brown_home_base", Middle = "giant_brown_middle", Crown = "giant_brown_crown", MostMiddles = 3 },
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
                    float top = AddPiece(root, basePiece, 0f, order++);
                    for (int i = 0; i < middles; i++)
                    {
                        // Brown trunks show their knot second from the bottom (or alone on a one-middle tree).
                        bool useKnot = knot != null && (middles == 1 || i % 2 == 1);
                        top = AddPiece(root, useKnot ? knot : middle, top, order++);
                    }
                    AddPiece(root, crown, top, order);

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
            return y + renderer.sprite.rect.height / renderer.sprite.pixelsPerUnit;
        }
    }
}
