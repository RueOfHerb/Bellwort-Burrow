using System.Collections.Generic;
using BellwortBurrow.EditorTools;
using BellwortBurrow.Tiles;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace BellwortBurrow.Tests.Editor
{
    public class StepsAndFacesTests
    {
        GameObject grid;
        Tilemap tilemap;
        Texture2D texture;
        readonly List<Object> made = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            grid = new GameObject("Grid", typeof(Grid));
            var layer = new GameObject("Banks", typeof(Tilemap), typeof(TilemapRenderer));
            layer.transform.SetParent(grid.transform);
            tilemap = layer.GetComponent<Tilemap>();
            texture = new Texture2D(16, 16);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var thing in made) if (thing != null) Object.DestroyImmediate(thing);
            made.Clear();
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(grid);
        }

        Sprite Named(string name)
        {
            var sprite = Sprite.Create(texture, new Rect(0, 0, 16, 16), Vector2.zero, 16);
            sprite.name = name;
            made.Add(sprite);
            return sprite;
        }

        T Face<T>(int rows) where T : FaceTile
        {
            var face = ScriptableObject.CreateInstance<T>();
            made.Add(face);
            var keys = GroundAutoTiles.FacePieceKeys();
            var sprites = new Sprite[keys.Count];
            for (int i = 0; i < keys.Count; i++) sprites[i] = Named(keys[i] + " #" + i);
            face.keys = keys.ToArray();
            face.sprites = sprites;
            face.rows = rows;
            return face;
        }

        StepsTile Steps()
        {
            var steps = ScriptableObject.CreateInstance<StepsTile>();
            made.Add(steps);
            steps.sprites = new Sprite[12];
            foreach (char row in StepsTile.PieceRows)
                foreach (char side in StepsTile.PieceSides)
                    steps.sprites[StepsTile.IndexOf(row, side)] = Named($"{row}{side}");
            return steps;
        }

        void Column(TileBase tile, int x, int top, int height)
        {
            for (int y = top; y > top - height; y--) tilemap.SetTile(new Vector3Int(x, y, 0), tile);
        }

        string KeyOf(int x, int y) => tilemap.GetSprite(new Vector3Int(x, y, 0)).name.Split(' ')[0];

        [Test]
        public void AStripFace_TakesItsLooksInOrderAlongTheFace_TheSameDownEachColumn()
        {
            var face = Face<FaceTile>(3);
            face.stripLooks = true;
            for (int x = -2; x < 16; x++) Column(face, x, 0, 3);
            string Look(int x, int y) => tilemap.GetSprite(new Vector3Int(x, y, 0)).name;
            int count = GroundAutoTiles.FacePieceKeys().FindAll(k => k == "FTcc").Count;
            for (int x = -1; x < 15; x++)
            {
                int v = ((x % count) + count) % count;
                StringAssert.StartsWith("FTcc", Look(x, 0));
                Assert.AreEqual(v, int.Parse(Look(x, 0).Split('#')[1]) - GroundAutoTiles.FacePieceKeys().IndexOf("FTcc"), $"column {x}'s top is look {v}");
                Assert.AreEqual(v, int.Parse(Look(x, -1).Split('#')[1]) - GroundAutoTiles.FacePieceKeys().IndexOf("FMcc"), $"column {x}'s middle matches its top");
                Assert.AreEqual(v, int.Parse(Look(x, -2).Split('#')[1]) - GroundAutoTiles.FacePieceKeys().IndexOf("FBcc"), $"column {x}'s foot matches its top");
            }
            string end = Look(-2, 0), endFoot = Look(-2, -2);
            Assert.AreEqual(int.Parse(end.Split('#')[1]) - GroundAutoTiles.FacePieceKeys().IndexOf("FToc"),
                            int.Parse(endFoot.Split('#')[1]) - GroundAutoTiles.FacePieceKeys().IndexOf("FBoc"), "an end keeps one look down its column");
        }

        [Test]
        public void FacesOfOneFamily_JoinWhereTheyMeet_OthersDoNot()
        {
            var bank = Face<FaceTile>(3); bank.family = "bank";
            var roots = Face<FaceTile>(3); roots.family = "bank";
            var cliff = Face<FaceTile>(3); cliff.family = "cliff";
            Column(bank, 0, 0, 3); Column(bank, 1, 0, 3);
            Column(roots, 2, 0, 3); Column(roots, 3, 0, 3);
            Column(cliff, 4, 0, 3);
            Assert.AreEqual("FTcc", KeyOf(1, 0), "the bank carries on into the roots");
            Assert.AreEqual("FTcc", KeyOf(2, 0), "the roots carry on from the bank");
            Assert.AreEqual("FTco", KeyOf(3, 0), "the roots end against the cliff");
            Assert.AreEqual("FToo", KeyOf(4, 0), "the cliff stands on its own");
        }

        [TestCase(false, true, false, true, 'T', 'l')]
        [TestCase(true, true, true, true, 'M', 'm')]
        [TestCase(true, false, true, false, 'B', 'r')]
        [TestCase(false, true, false, false, 'T', 's')]
        public void StepsPickTheirPiece_ByWhatIsAroundThem(bool n, bool s, bool w, bool e, char row, char side)
        {
            Assert.AreEqual((row, side), StepsTile.PieceFor(n, s, w, e));
        }

        [Test]
        public void StepsCutIntoAFace_BorderItAndGoUphill()
        {
            var face = Face<FaceTile>(3);
            var steps = Steps();
            Column(face, 0, 0, 3);
            Column(steps, 1, 0, 3); Column(steps, 2, 0, 3);
            Column(face, 3, 0, 3);
            Assert.AreEqual("FTob", KeyOf(0, 0), "the face west of the steps gets a border on its east");
            Assert.AreEqual("FBbo", KeyOf(3, -2), "and the face east of them on its west");
            Assert.AreEqual("Tl", tilemap.GetSprite(new Vector3Int(1, 0, 0)).name);
            Assert.AreEqual("Br", tilemap.GetSprite(new Vector3Int(2, -2, 0)).name);
            Assert.AreEqual(Vector2.up, steps.Uphill(tilemap, new Vector3Int(1, -1, 0)));
        }
    }
}
