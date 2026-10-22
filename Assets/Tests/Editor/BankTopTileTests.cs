using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;
using BellwortBurrow.EditorTools;
using BellwortBurrow.Tiles;

namespace BellwortBurrow.Tests.Editor
{
    public class BankTopTileTests
    {
        GameObject grid;
        Tilemap tilemap;
        BankTopTile top;
        FaceTile face;
        Texture2D texture;
        readonly List<Object> made = new List<Object>();

        static readonly Vector3Int[] Positions =
        {
            new Vector3Int(-1, 1, 0), new Vector3Int(0, 1, 0), new Vector3Int(1, 1, 0),
            new Vector3Int(-1, 0, 0), new Vector3Int(1, 0, 0),
            new Vector3Int(-1, -1, 0), new Vector3Int(0, -1, 0), new Vector3Int(1, -1, 0),
        };

        Sprite Named(string name)
        {
            var s = Sprite.Create(texture, new Rect(0, 0, 16, 16), Vector2.zero, 16);
            s.name = name; made.Add(s); return s;
        }

        [SetUp]
        public void SetUp()
        {
            grid = new GameObject("Grid", typeof(Grid));
            var layer = new GameObject("Banks", typeof(Tilemap), typeof(TilemapRenderer));
            layer.transform.SetParent(grid.transform);
            tilemap = layer.GetComponent<Tilemap>();
            texture = new Texture2D(16, 16);
            top = ScriptableObject.CreateInstance<BankTopTile>();
            var layouts = new HashSet<uint>();
            for (int i = 0; i < 256; i++)
            {
                uint around = 16;
                uint[] bits = { 1, 2, 4, 8, 32, 64, 128, 256 };
                for (int b = 0; b < 8; b++) if ((i & (1 << b)) != 0) around |= bits[b];
                layouts.Add(GroundAutoTiles.TopLayoutOf(around));
            }
            foreach (var layout in layouts)
            {
                var rule = new RuleTile.TilingRule { m_Sprites = new[] { Named("top " + layout) } };
                rule.m_NeighborPositions = new List<Vector3Int>(Positions);
                rule.m_Neighbors = new List<int>(GroundAutoTiles.TopRuleNeighbours(layout));
                top.m_TilingRules.Add(rule);
            }
            face = ScriptableObject.CreateInstance<FaceTile>();
            var keys = GroundAutoTiles.FacePieceKeys();
            var sprites = new Sprite[keys.Count];
            for (int i = 0; i < keys.Count; i++) sprites[i] = Named(keys[i]);
            face.keys = keys.ToArray();
            face.sprites = sprites;
            made.Add(top); made.Add(face); made.Add(texture);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(grid);
            foreach (var o in made) Object.DestroyImmediate(o);
            made.Clear();
        }

        uint LayoutOf(int x, int y) => uint.Parse(tilemap.GetSprite(new Vector3Int(x, y, 0)).name.Split(' ')[1]);

        [Test]
        public void ATopAboveA45DegreeRun_HasNoRimDownItsSide_ButATopBesideAStepKeepsIt()
        {
            // a 45 degree run down to the east, columns 1 and 2, between flat runs; the top above every column
            void Column(int x, int faceTop, int height)
            {
                for (int y = faceTop; y > faceTop - height; y--) tilemap.SetTile(new Vector3Int(x, y, 0), face);
                for (int y = faceTop + 1; y <= 3; y++) tilemap.SetTile(new Vector3Int(x, y, 0), top);
            }
            Column(0, 0, 3); Column(1, 0, 4); Column(2, -1, 4); Column(3, -2, 3);
            const uint W = 8;
            Assert.AreNotEqual(0u, LayoutOf(2, 0) & W, "above the run, the face's top cell to the west reads as top");
            Assert.AreNotEqual(0u, LayoutOf(3, -1) & W, "where the run meets flat");

            // a step: a block a row lower east of a flat block; its top beside the step keeps its rim
            Column(6, 0, 3); Column(7, 0, 3); Column(8, -1, 3); Column(9, -1, 3);
            Assert.AreEqual(0u, LayoutOf(8, 0) & W);
        }
    }
}
