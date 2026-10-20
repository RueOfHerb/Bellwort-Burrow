using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using BellwortBurrow.Data;
using BellwortBurrow.Player;

namespace BellwortBurrow.Tests.EditMode
{
    public class PlayerAppearanceTests
    {
        GameObject player;

        [TearDown]
        public void TearDown()
        {
            if (player != null) Object.DestroyImmediate(player);
        }

        static Sprite MakeSprite(string name)
        {
            var sprite = Sprite.Create(new Texture2D(2, 2), new Rect(0, 0, 2, 2), Vector2.zero);
            sprite.name = name;
            return sprite;
        }

        PlayerAppearance MakePlayer(out SpriteRenderer body, out Sprite foxSprite, out Sprite mouseSprite)
        {
            foxSprite = MakeSprite("fox");
            mouseSprite = MakeSprite("mouse");

            var fox = ScriptableObject.CreateInstance<CritterKind>();
            fox.ConfigureForTesting("fox", "Fox", foxSprite);
            var mouse = ScriptableObject.CreateInstance<CritterKind>();
            mouse.ConfigureForTesting("mouse", "Mouse", mouseSprite);
            var catalog = ScriptableObject.CreateInstance<CritterCatalog>();
            catalog.ConfigureForTesting(new[] { mouse, fox });

            player = new GameObject("Player");
            body = new GameObject("player_fox").AddComponent<SpriteRenderer>();
            body.transform.SetParent(player.transform);
            body.sprite = foxSprite;

            var appearance = player.AddComponent<PlayerAppearance>();
            appearance.ConfigureForTesting(catalog, body);
            return appearance;
        }

        [Test]
        public void Apply_ShowsTheProfilesCritterKind()
        {
            var appearance = MakePlayer(out var body, out _, out var mouseSprite);

            appearance.Apply(new PlayerProfile { critterKind = "mouse" });

            Assert.AreSame(mouseSprite, body.sprite);
            Assert.AreEqual("mouse", appearance.Kind.Id);
        }

        [Test]
        public void Apply_RecolorsThroughTheRampSwapMaterial()
        {
            var appearance = MakePlayer(out var body, out _, out _);
            var palette = ScriptableObject.CreateInstance<RampPalette>();
            palette.SetRamps(RampPalette.Parse("Columns: 2\n10 10 10\tOrange Shadow\n20 20 20\tOrange Base\n200 200 200\tWhite Shadow\n250 250 250\tWhite Base\n"));
            var fox = ScriptableObject.CreateInstance<CritterKind>();
            fox.ConfigureForTesting("fox", "Fox", body.sprite, "Orange", "", "");
            var material = new Material(Shader.Find("Sprites/Default"));
            var catalog = ScriptableObject.CreateInstance<CritterCatalog>();
            catalog.ConfigureForTesting(new[] { fox }, palette, material);
            appearance.ConfigureForTesting(catalog, body);

            appearance.Apply(new PlayerProfile { critterKind = "fox", furColor = "white" });

            Assert.AreSame(material, body.sharedMaterial);
            Assert.AreEqual(2, appearance.SwappedColors);
            var block = new MaterialPropertyBlock();
            body.GetPropertyBlock(block);
            Assert.AreEqual(2f, block.GetFloat("_SwapCount"));
            var from = block.GetVectorArray("_SwapFrom");
            var to = block.GetVectorArray("_SwapTo");
            Assert.AreEqual(10f / 255f, from[0].x, 0.0001f);
            Assert.AreEqual(250f / 255f, to[1].x, 0.0001f);
        }

        [Test]
        public void Apply_PutsTheFaceOnTop_AndABeakHidesTheMouth()
        {
            var appearance = MakePlayer(out var body, out var foxSprite, out _);
            var finch = ScriptableObject.CreateInstance<CritterKind>();
            finch.ConfigureForTesting("finch", "Finch", MakeSprite("finch"), showsMouth: false);
            var fox = ScriptableObject.CreateInstance<CritterKind>();
            fox.ConfigureForTesting("fox", "Fox", foxSprite);
            var faces = new[]
            {
                new FaceStyle("default", "Default", MakeSprite("face_default"), MakeSprite("face_default_mouth")),
                new FaceStyle("big-eyes", "Big eyes", MakeSprite("face_big_eyes"), MakeSprite("face_big_eyes_mouth")),
            };
            var catalog = ScriptableObject.CreateInstance<CritterCatalog>();
            catalog.ConfigureForTesting(new[] { fox, finch }, null, null, faces);
            appearance.ConfigureForTesting(catalog, body);

            appearance.Apply(new PlayerProfile { critterKind = "fox", face = "big-eyes" });

            Assert.AreSame(faces[1].eyes, appearance.FaceRenderer.sprite);
            Assert.AreSame(faces[1].mouth, appearance.MouthRenderer.sprite);
            Assert.Greater(appearance.FaceRenderer.sortingOrder, body.sortingOrder, "the face draws over the body");
            Assert.IsNotNull(appearance.GetComponent<SortingGroup>(), "the Player sorts as one picture");

            appearance.Apply(new PlayerProfile { critterKind = "finch", face = "otter-face" });

            Assert.AreEqual("default", appearance.Face.id, "an unknown face shows Default");
            Assert.AreSame(faces[0].eyes, appearance.FaceRenderer.sprite);
            Assert.IsNull(appearance.MouthRenderer.sprite, "the finch's beak hides the mouth");
        }

        [Test]
        public void Apply_DressesThePlayer_AFullOutfitCoversTheBottom_AndEarringsFindTheEars()
        {
            var appearance = MakePlayer(out var body, out var foxSprite, out _);
            var fox = ScriptableObject.CreateInstance<CritterKind>();
            fox.ConfigureForTesting("fox", "Fox", foxSprite, earAnchor: new Vector2Int(5, 9));
            var mouse = ScriptableObject.CreateInstance<CritterKind>();
            mouse.ConfigureForTesting("mouse", "Mouse", MakeSprite("mouse"), earAnchor: new Vector2Int(3, 11));
            var finch = ScriptableObject.CreateInstance<CritterKind>();
            finch.ConfigureForTesting("finch", "Finch", MakeSprite("finch"), hasEars: false);
            var tunic = new ClothingPiece("tunic", "Tunic", ClothingSlot.Top, MakeSprite("top_tunic"), "Blue");
            var dress = new ClothingPiece("dress", "Dress", ClothingSlot.Top, MakeSprite("top_dress"), "Purple", coversBottom: true);
            var trousers = new ClothingPiece("trousers", "Trousers", ClothingSlot.Bottom, MakeSprite("bottom_trousers"), "Taupe");
            var studs = new ClothingPiece("studs", "Studs", ClothingSlot.Ears, MakeSprite("ears_studs"), paired: true);
            var flower = new ClothingPiece("flower", "Flower", ClothingSlot.Ears, MakeSprite("ears_flower"));
            var noEars = new ClothingPiece("none", "None", ClothingSlot.Ears, null);
            var wardrobe = ScriptableObject.CreateInstance<Wardrobe>();
            wardrobe.ConfigureForTesting(new[] { tunic, dress, trousers, noEars, studs, flower }, new Vector2Int(5, 9));
            var catalog = ScriptableObject.CreateInstance<CritterCatalog>();
            catalog.ConfigureForTesting(new[] { fox, mouse, finch }, wardrobe: wardrobe);
            appearance.ConfigureForTesting(catalog, body);

            appearance.Apply(new PlayerProfile { critterKind = "fox" });
            Assert.AreSame(tunic.sprite, appearance.TopRenderer.sprite, "a new character wears the first top");
            Assert.AreSame(trousers.sprite, appearance.BottomRenderer.sprite);
            Assert.IsNull(appearance.EarLeftRenderer.sprite, "and nothing on its ears");
            Assert.Greater(appearance.TopRenderer.sortingOrder, appearance.BottomRenderer.sortingOrder, "the top goes over the bottom");

            appearance.Apply(new PlayerProfile { critterKind = "mouse", top = "dress", bottom = "trousers", ears = "studs" });
            Assert.AreSame(dress.sprite, appearance.TopRenderer.sprite);
            Assert.IsNull(appearance.BottomRenderer.sprite, "the dress covers the bottom");
            Assert.AreSame(studs.sprite, appearance.EarLeftRenderer.sprite);
            Assert.AreSame(studs.sprite, appearance.EarRightRenderer.sprite, "studs come in a pair");
            Assert.IsTrue(appearance.EarRightRenderer.flipX);
            Assert.AreEqual(new Vector3(-2f, -2f, 0f) / 16f, appearance.EarLeftRenderer.transform.localPosition, "moved to the mouse's lower, wider ears");
            Assert.AreEqual(new Vector3(2f, -2f, 0f) / 16f, appearance.EarRightRenderer.transform.localPosition, "and mirrored for the right one");

            appearance.Apply(new PlayerProfile { critterKind = "mouse", ears = "flower" });
            Assert.AreSame(flower.sprite, appearance.EarLeftRenderer.sprite);
            Assert.IsNull(appearance.EarRightRenderer.sprite, "a flower goes on one ear");

            appearance.Apply(new PlayerProfile { critterKind = "finch", ears = "studs" });
            Assert.IsNull(appearance.EarLeftRenderer.sprite, "the finch has no ears for earrings");
            Assert.IsNull(appearance.EarRightRenderer.sprite);
        }

        [Test]
        public void Apply_AnUnknownKind_LeavesThePlayerAsPlaced()
        {
            var appearance = MakePlayer(out var body, out var foxSprite, out _);

            appearance.Apply(new PlayerProfile { critterKind = "otter" });

            Assert.AreSame(foxSprite, body.sprite);
            Assert.IsNull(appearance.Kind);
        }
    }
}
