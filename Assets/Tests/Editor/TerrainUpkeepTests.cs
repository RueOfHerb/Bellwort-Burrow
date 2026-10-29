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
            Assert.That(odd[0].why, Does.Contain("sheer drop"));
        }

        [Test]
        public void AColumnTooTallIsNamed()
        {
            Column(0, 10, 4);
            var odd = TerrainUpkeep.OddShapes(map);
            Assert.That(odd.Count, Is.EqualTo(1));
            Assert.That(odd[0].cell, Is.EqualTo(new Vector3Int(0, 10, 0)));
        }
    }
}
