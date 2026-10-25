using System.Collections.Generic;
using System.Linq;
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
        public void AStretchingRamp_KeepsItsEdgesEndAndFace_AndRepeatsTheRest()
        {
            var north = Ramp(4, 7, Vector2.up, 0);
            north.stretches = true; north.faceRows = 3; north.steepness = 0.75f;
            Block(north, 0, 0, 6, 10);   // 6 wide, 10 tall: x 0 to 5, y 0 down to -9
            string At(int x, int y) => tilemap.GetSprite(new Vector3Int(x, y, 0)).name;
            Assert.AreEqual("ramp 0", At(0, 0), "the back end's west edge");
            Assert.AreEqual("ramp 3", At(5, 0), "the back end's east edge");
            Assert.AreEqual("ramp 1", At(1, 0), "a middle column");
            Assert.AreEqual("ramp 2", At(2, 0), "the next middle column");
            Assert.AreEqual("ramp 1", At(3, 0), "the middle columns take turns");
            Assert.AreEqual("ramp 24", At(0, -9), "the face's foot, west edge");
            Assert.AreEqual("ramp 16", At(0, -7), "the face's top row");
            Assert.AreEqual("ramp 5", At(1, -1), "the first deck row");
            Assert.AreEqual("ramp 9", At(1, -2), "the second deck row");
            Assert.AreEqual("ramp 5", At(1, -3), "the deck rows take turns");
            var longer = north.Uphill(tilemap, new Vector3Int(2, -4, 0));
            Assert.AreEqual(0.75f * 4 / 7, longer.y, 1e-4f, "7 rows of deck instead of 4: gentler");
        }

        [Test]
        public void CloseBankSteps_TurnsEachOneRowStep45Degrees()
        {
            var face = Face<FaceTile>(3);
            Column(face, 0, 0, 3); Column(face, 1, 0, 3);
            Column(face, 2, -1, 3); Column(face, 3, -1, 3);    // a step down to the east
            Column(face, 4, 0, 3);                             // and back up
            var added = BankTools.CloseSteps(tilemap);
            CollectionAssert.AreEquivalent(new[] { new Vector3Int(2, 0, 0), new Vector3Int(3, 0, 0) }, added);
            StringAssert.StartsWith("D", KeyOf(2, 0), "the step down is a 45 degree column");
            StringAssert.StartsWith("U", KeyOf(3, 0), "and so is the step back up");
            Assert.AreEqual(0, BankTools.CloseSteps(tilemap).Count, "closing twice changes nothing");
        }

        [Test]
        public void TheRampBrush_SitsTheBlockOnTheFacesFoot()
        {
            var face = Face<FaceTile>(3);
            for (int x = 0; x < 10; x++) Column(face, x, 0, 3);
            var ramps = new GameObject("Ramps", typeof(Tilemap)).GetComponent<Tilemap>();
            ramps.transform.SetParent(grid.transform);
            var run = RampBrush.FaceRunNear(ramps, new Vector3Int(5, 2, 0));
            Assert.AreEqual((0, -2), run, "from the Ramps Tilemap, a click just above the face finds the bank under it");
            var east = Ramp(6, 7, Vector2.right, 1);
            Assert.AreEqual(new RectInt(3, 4, 6, 7), RampBrush.BlockFor(RampBrush.Kind.East, east, 4, 7, 5, run.Value), "6 by 7, its bottom row on the foot");
            var north = Ramp(4, 7, Vector2.up, 0); north.stretches = true;
            Assert.AreEqual(new RectInt(4, 7, 4, 10), RampBrush.BlockFor(RampBrush.Kind.North, north, 4, 10, 5, run.Value), "a North ramp takes the width and length set");
            Assert.AreEqual(new RectInt(4, 0, 3, 3), RampBrush.BlockFor(RampBrush.Kind.Steps, Steps(), 3, 7, 5, run.Value), "steps take the face's rows");
        }

        [Test]
        public void TheFaceUnderARampOrSteps_StopsBlocking_AndKeepsItsLooks()
        {
            var face = Face<FaceTile>(3);
            for (int x = 0; x < 6; x++) Column(face, x, 0, 3);
            var ramps = new GameObject("Ramps", typeof(Tilemap)).GetComponent<Tilemap>();
            ramps.transform.SetParent(grid.transform);
            string before = KeyOf(2, -1);
            Assert.AreEqual(Tile.ColliderType.Sprite, tilemap.GetColliderType(new Vector3Int(2, -1, 0)));
            var steps = Steps();
            for (int y = 0; y >= -2; y--) for (int x = 2; x <= 3; x++) ramps.SetTile(new Vector3Int(x, y, 0), steps);
            tilemap.RefreshAllTiles();
            Assert.AreEqual(Tile.ColliderType.None, tilemap.GetColliderType(new Vector3Int(2, -1, 0)), "under the steps");
            Assert.AreEqual(Tile.ColliderType.Sprite, tilemap.GetColliderType(new Vector3Int(4, -1, 0)), "beside them it still blocks");
            Assert.AreEqual(before, KeyOf(2, -1), "and the face looks the same");
            Assert.AreEqual("FMcc", KeyOf(1, -1), "the face beside them carries on, with no border");
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
