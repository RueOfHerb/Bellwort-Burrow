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
        public void Apply_AnUnknownKind_LeavesThePlayerAsPlaced()
        {
            var appearance = MakePlayer(out var body, out var foxSprite, out _);

            appearance.Apply(new PlayerProfile { critterKind = "otter" });

            Assert.AreSame(foxSprite, body.sprite);
            Assert.IsNull(appearance.Kind);
        }
    }
}
