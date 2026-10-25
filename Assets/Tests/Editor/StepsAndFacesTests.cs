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

        [Test]
        public void ALone45DegreeColumn_IsAJogInTheBank_WithNoCreaseBesideIt()
        {
            var face = Face<FaceTile>(3);
            Column(face, 0, 0, 3); Column(face, 1, 0, 3);
            Column(face, 2, 0, 4);
            Column(face, 3, -1, 3); Column(face, 4, -1, 3);
            Assert.AreEqual("FTcc", KeyOf(3, -1), "the flat run after the jog carries straight on from it");
            Assert.AreEqual("FTcc", KeyOf(1, 0), "and the one before it");
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

        RampTile Ramp(int columns, int rows, Vector2 uphill, int landing)
        {
            var ramp = ScriptableObject.CreateInstance<RampTile>();
            made.Add(ramp);
            ramp.columns = columns; ramp.rows = rows; ramp.uphill = uphill; ramp.landing = landing;
            ramp.steepness = 0.6f; ramp.lift = 0.6f;
            ramp.sprites = new Sprite[columns * rows];
            for (int i = 0; i < ramp.sprites.Length; i++) ramp.sprites[i] = Named("ramp " + i);
            return ramp;
        }

        void Block(TileBase tile, int left, int top, int columns, int rows)
        {
            for (int y = top; y > top - rows; y--)
                for (int x = left; x < left + columns; x++) tilemap.SetTile(new Vector3Int(x, y, 0), tile);
        }

        [Test]
        public void ARampBlock_ShowsItsPicturePieceByPiece()
        {
            var ramp = Ramp(6, 5, Vector2.right, 1);
            Block(ramp, 3, 4, 6, 5);
            Assert.AreEqual("ramp 0", tilemap.GetSprite(new Vector3Int(3, 4, 0)).name, "the top left");
            Assert.AreEqual("ramp 5", tilemap.GetSprite(new Vector3Int(8, 4, 0)).name, "the top right");
            Assert.AreEqual("ramp 13", tilemap.GetSprite(new Vector3Int(4, 2, 0)).name, "row 2, column 1");
            Assert.AreEqual("ramp 29", tilemap.GetSprite(new Vector3Int(8, 0, 0)).name, "the bottom right");
        }

        [Test]
        public void ARampCutIntoAFace_LetsTheFaceCarryOnIntoIt()
        {
            var face = Face<FaceTile>(3);
            var ramp = Ramp(6, 5, Vector2.right, 1);
            Column(face, 0, 0, 3);
            Block(ramp, 1, 2, 6, 5);
            Column(face, 7, 0, 3);
            Assert.AreEqual("FToc", KeyOf(0, 0), "west of the ramp the face carries on east");
            Assert.AreEqual("FBco", KeyOf(7, -2), "and east of it, west");
        }

        [Test]
        public void ARampAlongTheFace_IsLevelOnItsLanding_ThenClimbsAndLifts()
        {
            var east = Ramp(6, 5, Vector2.right, 1);
            Block(east, 0, 0, 6, 5);
            Assert.AreEqual(Vector2.zero, east.Uphill(tilemap, new Vector3Int(0, -4, 0)), "the landing is level");
            Assert.AreEqual(0f, east.Rise(tilemap, new Vector3Int(0, -4, 0)));
            Assert.AreEqual(Vector2.right * 0.6f, east.Uphill(tilemap, new Vector3Int(3, -2, 0)));
            Assert.AreEqual(0.6f, east.Rise(tilemap, new Vector3Int(3, -2, 0)), 1e-5f, "walking east lifts up the screen");

            var west = Ramp(6, 5, Vector2.left, 1);
            Block(west, 10, 0, 6, 5);
            Assert.AreEqual(Vector2.zero, west.Uphill(tilemap, new Vector3Int(15, -4, 0)), "its landing is at the east end");
            Assert.AreEqual(-0.6f, west.Rise(tilemap, new Vector3Int(12, -2, 0)), 1e-5f, "walking west lifts up the screen");

            var north = Ramp(2, 7, Vector2.up, 0);
            north.steepness = 0.75f; north.lift = 0f;
            Block(north, 20, 0, 2, 7);
            Assert.AreEqual(Vector2.up * 0.75f, north.Uphill(tilemap, new Vector3Int(20, -6, 0)));
            Assert.AreEqual(0f, north.Rise(tilemap, new Vector3Int(21, -3, 0)));
        }

        [Test]
        public void ARampTile_BlocksAboveAndBelowItsWalkway_WithStraightEdges()
        {
            // a walkway climbing one pixel every two, eight pixels thick
            System.Func<int, int, bool> solid = (x, y) => !(y >= x / 2 + 2 && y < x / 2 + 10);
            var shapes = Colliders.CutShape(solid, 16);
            Assert.AreEqual(2, shapes.Count, "above the walkway and below it");
            foreach (var shape in shapes)
                Assert.LessOrEqual(shape.Length, 6, "the walkway's edges come out as straight lines, not pixel steps");
            Assert.AreEqual(0, Colliders.CutShape((x, y) => false, 16).Count, "all walkway: nothing blocks");
            Assert.AreEqual(1, Colliders.CutShape((x, y) => true, 16).Count, "no walkway: the whole tile blocks");
        }
    }
}
