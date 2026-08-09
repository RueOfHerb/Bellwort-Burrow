using NUnit.Framework;
using UnityEngine;
using BellwortBurrow.Data;
using BellwortBurrow.Systems.Dialogue;

namespace BellwortBurrow.Tests.EditMode
{
    public class DialogueServiceTests
    {
        [Test]
        public void TryAdvance_WalksThroughLinesAndStopsAtEnd()
        {
            var dialogue = ScriptableObject.CreateInstance<DialogueDefinition>();
            dialogue.ConfigureForTesting("line1", new[]
            {
                new DialogueLine { id = "line1", speakerId = "npc", text = "Hello", nextLineId = "line2" },
                new DialogueLine { id = "line2", speakerId = "npc", text = "Bye", nextLineId = null },
            });

            var service = new DialogueService();

            bool started = service.TryStart(dialogue, out var first);
            Assert.IsTrue(started);
            Assert.AreEqual("line1", first.id);

            bool advanced = service.TryAdvance(dialogue, "line1", out var second);
            Assert.IsTrue(advanced);
            Assert.AreEqual("line2", second.id);

            bool advancedPastEnd = service.TryAdvance(dialogue, "line2", out _);
            Assert.IsFalse(advancedPastEnd);
        }
    }
}
