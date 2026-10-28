using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;
using BellwortBurrow.Tiles;

namespace BellwortBurrow.Tests.Editor
{
    public class GlintRuleTileTests
    {
        GameObject grid, glintObject;
        Tilemap tilemap;
        Texture2D texture;
        Sprite plain, specked, glint;
        GlintRuleTile tile;

        [SetUp]
        public void SetUp()
        {
            grid = new GameObject("Grid", typeof(Grid));
            var layer = new GameObject("Ground", typeof(Tilemap), typeof(TilemapRenderer));
            layer.transform.SetParent(grid.transform);
            tilemap = layer.GetComponent<Tilemap>();
            var renderer = layer.GetComponent<TilemapRenderer>();
            renderer.sortingOrder = -40;
            texture = new Texture2D(48, 16);
            plain = Sprite.Create(texture, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16);
            specked = Sprite.Create(texture, new Rect(16, 0, 16, 16), new Vector2(0.5f, 0.5f), 16);
            glint = Sprite.Create(texture, new Rect(32, 0, 16, 16), new Vector2(0.5f, 0.5f), 16);
            glintObject = new GameObject("Tile Glint", typeof(SpriteRenderer));
            tile = ScriptableObject.CreateInstance<GlintRuleTile>();
            tile.glintObject = glintObject;
            tile.glintPieces.Add(specked);
            tile.glints.Add(glint);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(grid);
            Object.DestroyImmediate(glintObject);
            Object.DestroyImmediate(tile);
            Object.DestroyImmediate(plain);
            Object.DestroyImmediate(specked);
            Object.DestroyImmediate(glint);
            Object.DestroyImmediate(texture);
        }

        [Test]
        public void GlintFor_PairsAPieceWithItsGlint()
        {
            Assert.AreSame(glint, tile.GlintFor(specked));
            Assert.IsNull(tile.GlintFor(plain));
            Assert.IsNull(tile.GlintFor(null));
        }

        [Test]
        public void APieceWithAGlint_GetsItUnlitOnTop()
        {
            tile.m_DefaultSprite = specked;
            tilemap.SetTile(Vector3Int.zero, tile);
            var placed = tilemap.GetInstantiatedObject(Vector3Int.zero);
            Assert.IsNotNull(placed, "a specked piece gets its glint object");
            var picture = placed.GetComponent<SpriteRenderer>();
            Assert.AreSame(glint, picture.sprite);
            Assert.AreEqual(-39, picture.sortingOrder, "the glint draws one step above the ground");
        }

        [Test]
        public void APieceWithoutAGlint_GetsNothing()
        {
            tile.m_DefaultSprite = plain;
            tilemap.SetTile(Vector3Int.zero, tile);
            Assert.IsNull(tilemap.GetInstantiatedObject(Vector3Int.zero));
        }
    }
}
