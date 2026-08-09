using NUnit.Framework;
using BellwortBurrow.Systems.Relationships;

namespace BellwortBurrow.Tests.EditMode
{
    public class RelationshipServiceTests
    {
        [Test]
        public void AddFriendship_AccumulatesAndRaisesOnFriendshipChanged()
        {
            var service = new RelationshipService();
            int changedCount = 0;
            string lastNpcId = null;
            int lastValue = 0;
            service.OnFriendshipChanged += (npcId, value) =>
            {
                changedCount++;
                lastNpcId = npcId;
                lastValue = value;
            };

            service.AddFriendship("Nadia", 5);
            service.AddFriendship("Nadia", 3);

            Assert.AreEqual(8, service.GetFriendship("Nadia"));
            Assert.AreEqual(2, changedCount);
            Assert.AreEqual("Nadia", lastNpcId);
            Assert.AreEqual(8, lastValue);
            Assert.AreEqual(0, service.GetFriendship("Unregistered"));
        }
    }
}
