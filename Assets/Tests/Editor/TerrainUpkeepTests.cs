using System.Linq;
using BellwortBurrow.EditorTools;
using BellwortBurrow.Tiles;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace BellwortBurrow.Tests.Editor
{
    /// <summary>Terrain upkeep (arc-11): which ground sheets it remakes tiles for, and the bank shapes it warns about.</summary>
    public class TerrainUpkeepTests
    {
        GameObject grid;
        Tilemap map;
        FaceTile face;

        [SetUp]
        public void SetUp()
        {
            grid = new GameObject("Grid", typeof(Grid));
            var layer = new GameObject("Faces", typeof(Tilemap));
            layer.transform.SetParent(grid.transform);
            map = layer.GetComponent<Tilemap>();
            face = ScriptableObject.CreateInstance<FaceTile>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(grid);
            Object.DestroyImmediate(face);
        }

        void Column(int x, int top, int bottom)
        {
            for (int y = bottom; y <= top; y++) map.SetTile(new Vector3Int(x, y, 0), face);
        }

        [Test]
        public void OnlyGroundSheetsUnderTheArtFolderCount()
        {
            Assert.That(TerrainUpkeep.IsGroundSheet("Assets/Art/Burrow/burrow_ground.aseprite"));
            Assert.That(TerrainUpkeep.IsGroundSheet("Assets/Art/Forest/forest_ground.aseprite"));
            Assert.That(TerrainUpkeep.IsGroundSheet("Assets/Art/Burrow/burrow_stump.aseprite"), Is.False);
            Assert.That(TerrainUpkeep.IsGroundSheet("Assets/Art/Burrow/burrow_ground.png"), Is.False);
            Assert.That(TerrainUpkeep.IsGroundSheet("tmp/burrow_ground.aseprite"), Is.False);
        }

        [Test]
        public void ACleanStaircaseAndAFlatStepAreFine()
        {
            for (int i = 0; i < 4; i++) Column(i, 20 - i, 17 - i);   // a 45 degree run down to the east
            Column(4, 16, 14); Column(5, 16, 14);                     // carrying on flat
            Column(6, 18, 16); Column(7, 18, 16);                     // a step up two rows
            Assert.That(TerrainUpkeep.OddShapes(map), Is.Empty);
        }

        [Test]
        public void ASheerDropInA45DegreeRunIsNamed()
        {
            // the Bellwort Burrow bank at x 107 and 108 (Oct 29): a 45 degree column, then the next one starting 4 rows lower
            Column(0, 48, 45); Column(1, 47, 44); Column(2, 43, 40); Column(3, 42, 39);
            var odd = TerrainUpkeep.OddShapes(map);
            Assert.That(odd.Select(o => o.cell), Is.EquivalentTo(new[] { new Vector3Int(1, 47, 0), new Vector3Int(2, 43, 0) }));
            Assert.That(odd[0].why, Does.Contain("steeper than 45 degrees"));
        }

        [Test]
        public void AColumnTooTallIsNamed()
        {
            Column(0, 10, 4);
            var odd = TerrainUpkeep.OddShapes(map);
            Assert.That(odd.Count, Is.EqualTo(1));
            Assert.That(odd[0].cell, Is.EqualTo(new Vector3Int(0, 10, 0)));
        }

        [Test]
        public void StraighteningASteepRunKeepsItsTopAndLeavesOnly45Degrees()
        {
            Column(-1, 48, 46);                                          // flat, then down: steeply at x 1 to 2
            Column(0, 48, 45); Column(1, 47, 44); Column(2, 43, 40); Column(3, 42, 39);
            Column(4, 41, 39); Column(5, 41, 39); Column(6, 41, 39); Column(7, 41, 39);   // flat again, long enough to catch up
            BankTools.StraightenBanks(map);
            Assert.That(TerrainUpkeep.OddShapes(map), Is.Empty);
            for (int y = 46; y <= 48; y++) Assert.That(map.GetTile(new Vector3Int(-1, y, 0)), Is.Not.Null, "the high end stays");
            Assert.That(map.GetTile(new Vector3Int(2, 46, 0)), Is.Not.Null, "x 2 now carries on a row below x 1");
            Assert.That(map.GetTile(new Vector3Int(7, 41, 0)), Is.Not.Null, "and the flat run is met again");
            Assert.That(map.GetTile(new Vector3Int(7, 42, 0)), Is.Null);
        }

        [Test]
        public void TuftsOnWaterOrAPathAreCleared()
        {
            var tufts = new GameObject("Tufts and Crop Circles", typeof(Tilemap)).GetComponent<Tilemap>();
            var water = new GameObject("Water", typeof(Tilemap)).GetComponent<Tilemap>();
            tufts.transform.SetParent(grid.transform); water.transform.SetParent(grid.transform);
            var tuft = ScriptableObject.CreateInstance<Tile>();
            var wet = ScriptableObject.CreateInstance<Tile>();
            tufts.SetTile(new Vector3Int(1, 1, 0), tuft);
            tufts.SetTile(new Vector3Int(2, 1, 0), tuft);
            water.SetTile(new Vector3Int(2, 1, 0), wet);
            Assert.That(TerrainUpkeep.TuftsBeside(water), Is.EqualTo(tufts));
            var gone = TerrainUpkeep.ClearTuftsOver(tufts, new[] { water });
            Assert.That(gone, Is.EqualTo(new[] { new Vector3Int(2, 1, 0) }));
            Assert.That(tufts.GetTile(new Vector3Int(1, 1, 0)), Is.Not.Null, "a tuft on grass stays");
            Object.DestroyImmediate(tuft); Object.DestroyImmediate(wet);
        }
    }
}
