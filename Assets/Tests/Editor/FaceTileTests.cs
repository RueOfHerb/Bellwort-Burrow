using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;
using BellwortBurrow.Tiles;

namespace BellwortBurrow.Tests.Editor
{
    public class FaceTileTests
    {
        GameObject grid;
        Tilemap tilemap;
        FaceTile face;
        Texture2D texture;

        [SetUp]
        public void SetUp()
        {
            grid = new GameObject("Grid", typeof(Grid));
            var layer = new GameObject("Banks", typeof(Tilemap), typeof(TilemapRenderer));
            layer.transform.SetParent(grid.transform);
            tilemap = layer.GetComponent<Tilemap>();
            texture = new Texture2D(16, 16);
            face = ScriptableObject.CreateInstance<FaceTile>();
            face.pieces = new Sprite[FaceTile.Layouts * FaceTile.Columns * FaceTile.Rows];
            for (int i = 0; i < face.pieces.Length; i++)
            {
                face.pieces[i] = Sprite.Create(texture, new Rect(0, 0, 16, 16), Vector2.zero, 16);
                face.pieces[i].name = "piece " + i;
            }
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var piece in face.pieces) Object.DestroyImmediate(piece);
            Object.DestroyImmediate(face);
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(grid);
        }

        Sprite Expected(int layout, int x, int y)
        {
            var (column, row) = FaceTile.Phase(new Vector3Int(x, y, 0));
            return face.pieces[FaceTile.Index(layout, column, row)];
        }

        [Test]
        public void Phase_CountsColumnsAcrossAndRowsDown()
        {
            Assert.AreEqual((0, 0), FaceTile.Phase(new Vector3Int(0, 0, 0)));
            Assert.AreEqual((1, 1), FaceTile.Phase(new Vector3Int(1, -1, 0)));
            Assert.AreEqual((2, 1), FaceTile.Phase(new Vector3Int(-1, 1, 0)));
            Assert.AreEqual((0, 0), FaceTile.Phase(new Vector3Int(3, -2, 0)));
        }

        [Test]
        public void EachCell_PicksItsPieceBySidesAndPhase()
        {
            foreach (var (x, y) in new[] { (0, 0), (1, 0), (0, -1), (1, -1) })
                tilemap.SetTile(new Vector3Int(x, y, 0), face);
            Assert.AreEqual(Expected(FaceTile.South | FaceTile.East, 0, 0), tilemap.GetSprite(new Vector3Int(0, 0, 0)), "top left");
            Assert.AreEqual(Expected(FaceTile.South | FaceTile.West, 1, 0), tilemap.GetSprite(new Vector3Int(1, 0, 0)), "top right");
            Assert.AreEqual(Expected(FaceTile.North | FaceTile.East, 0, -1), tilemap.GetSprite(new Vector3Int(0, -1, 0)), "bottom left");
            Assert.AreEqual(Expected(FaceTile.North | FaceTile.West, 1, -1), tilemap.GetSprite(new Vector3Int(1, -1, 0)), "bottom right");

            tilemap.SetTile(new Vector3Int(1, 0, 0), null);
            Assert.AreEqual(Expected(FaceTile.South, 0, 0), tilemap.GetSprite(new Vector3Int(0, 0, 0)), "erasing a cell turns the face beside it");
            Assert.AreEqual(Expected(FaceTile.North | FaceTile.East, 0, -1), tilemap.GetSprite(new Vector3Int(0, -1, 0)));
        }

        [Test]
        public void AMissingPiece_FallsBackToTheSameLayoutAtAnotherPhase()
        {
            int layout = FaceTile.North | FaceTile.South;
            face.pieces[FaceTile.Index(layout, 2, 1)] = null;
            Assert.AreEqual(face.pieces[FaceTile.Index(layout, 0, 0)], face.PieceFor(layout, 2, 1));
        }
    }
}
