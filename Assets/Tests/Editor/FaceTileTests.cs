using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;
using BellwortBurrow.EditorTools;
using BellwortBurrow.Tiles;

namespace BellwortBurrow.Tests.Editor
{
    public class FaceTileTests
    {
        GameObject grid;
        Tilemap tilemap;
        FaceTile face;
        Texture2D texture;
        readonly List<Sprite> made = new List<Sprite>();

        [SetUp]
        public void SetUp()
        {
            grid = new GameObject("Grid", typeof(Grid));
            var layer = new GameObject("Banks", typeof(Tilemap), typeof(TilemapRenderer));
            layer.transform.SetParent(grid.transform);
            tilemap = layer.GetComponent<Tilemap>();
            texture = new Texture2D(16, 16);
            face = ScriptableObject.CreateInstance<FaceTile>();
            var keys = GroundAutoTiles.FacePieceKeys();
            var sprites = new Sprite[keys.Count];
            for (int i = 0; i < keys.Count; i++)
            {
                sprites[i] = Sprite.Create(texture, new Rect(0, 0, 16, 16), Vector2.zero, 16);
                sprites[i].name = keys[i] + " #" + i;
                made.Add(sprites[i]);
            }
            face.keys = keys.ToArray();
            face.sprites = sprites;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var s in made) Object.DestroyImmediate(s);
            made.Clear();
            Object.DestroyImmediate(face);
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(grid);
        }

        // A column of face from top down to bottom (y up).
        void Column(int x, int top, int height)
        {
            for (int y = top; y > top - height; y--) tilemap.SetTile(new Vector3Int(x, y, 0), face);
        }

        string KeyOf(int x, int y) => tilemap.GetSprite(new Vector3Int(x, y, 0)).name.Split(' ')[0];

        [Test]
        public void AFlatBank_HasALipAFootAndOpenEnds()
        {
            for (int x = 0; x < 4; x++) Column(x, 0, 3);
            Assert.AreEqual("FToc", KeyOf(0, 0), "top left");
            Assert.AreEqual("FMcc", KeyOf(1, -1), "middle");
            Assert.AreEqual("FBco", KeyOf(3, -2), "bottom right");
        }

        [Test]
        public void AStepDown_BordersTheBlockInFront()
        {
            Column(0, 0, 3); Column(1, 0, 3);           // the block further north
            Column(2, -1, 3); Column(3, -1, 3);         // a step down: further south, in front
            Assert.AreEqual("FTbc", KeyOf(2, -1), "the front block's west end has its border");
            Assert.AreEqual("FToc", KeyOf(0, 0));
            Assert.AreEqual("FTcc", KeyOf(1, 0), "the block behind carries on behind it");
        }

        [Test]
        public void AStepUp_BordersTheBlockInFrontOnItsEast()
        {
            Column(0, -1, 3); Column(1, -1, 3);
            Column(2, 0, 3); Column(3, 0, 3);
            Assert.AreEqual("FTcb", KeyOf(1, -1));
            Assert.AreEqual("FTcc", KeyOf(2, 0));
        }

        [Test]
        public void AOneForOneStaircase_RunsAt45Degrees_WithACreaseAtItsFoot()
        {
            Column(0, 0, 3);                            // flat
            Column(1, 0, 4); Column(2, -1, 4);          // 45 degrees down to the east: a column four tall, a row lower each
            Column(3, -2, 3); Column(4, -2, 3);         // flat again, a row below the last 45 degree column's top
            Assert.AreEqual("D0cc", KeyOf(1, 0));
            Assert.AreEqual("D3cc", KeyOf(1, -3));
            Assert.AreEqual("D1cc", KeyOf(2, -2));
            Assert.AreEqual("FTkc", KeyOf(3, -2), "the inner corner at the foot of the run");
            Assert.AreEqual("FTco", KeyOf(4, -2));
            Assert.AreEqual("FToc", KeyOf(0, 0), "an outer corner needs nothing");
        }

        [Test]
        public void AStaircaseUp_RunsAt45DegreesTheOtherWay_WithACreaseAtItsStart()
        {
            Column(0, 0, 3);
            Column(1, 1, 4); Column(2, 2, 4);
            Column(3, 2, 3);
            Assert.AreEqual("FTok", KeyOf(0, 0), "the inner corner where the run up starts");
            Assert.AreEqual("U0cc", KeyOf(1, 1));
            Assert.AreEqual("U2cc", KeyOf(2, 0));
            Assert.AreEqual("FTco", KeyOf(3, 2));
        }

        [Test]
        public void AFortyFiveDegreeColumn_UsesOneVariationAllTheWayDown()
        {
            Column(0, 0, 3); Column(1, 0, 4); Column(2, -1, 4); Column(3, -2, 3);
            int Variation(int y) => int.Parse(tilemap.GetSprite(new Vector3Int(1, y, 0)).name.Split('#')[1]) -
                                    GroundAutoTiles.FacePieceKeys().IndexOf(KeyOf(1, y));
            Assert.AreEqual(Variation(0), Variation(-1));
            Assert.AreEqual(Variation(0), Variation(-3));
        }

        [Test]
        public void AMissingLook_FallsBackToAPlainerOne()
        {
            Assert.AreEqual("FMcc", face.PieceFor("FMxx", Vector2Int.zero).name.Split(' ')[0]);
            Assert.IsNull(face.PieceFor(null, Vector2Int.zero), "no key, no default: nothing");
        }
    }
}
