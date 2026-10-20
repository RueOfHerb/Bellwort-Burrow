using NUnit.Framework;
using UnityEngine;
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
        public void Apply_AnUnknownKind_LeavesThePlayerAsPlaced()
        {
            var appearance = MakePlayer(out var body, out var foxSprite, out _);

            appearance.Apply(new PlayerProfile { critterKind = "otter" });

            Assert.AreSame(foxSprite, body.sprite);
            Assert.IsNull(appearance.Kind);
        }
    }
}
