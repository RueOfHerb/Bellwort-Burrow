using NUnit.Framework;
using BellwortBurrow.Systems.Relationships;
using BellwortBurrow.Systems.Save;

namespace BellwortBurrow.Tests.EditMode
{
    public class SaveServiceTests
    {
        [Test]
        public void SaveAllThenLoadAll_RestoresSaveableStateFromSnapshot()
        {
            var relationships = new RelationshipService();
            var saveService = new SaveService();
            saveService.Register(relationships);

            relationships.AddFriendship("Nadia", 10);
            saveService.SaveAll();
            relationships.AddFriendship("Nadia", 50);

            saveService.LoadAll();

            Assert.AreEqual(10, relationships.GetFriendship("Nadia"));
        }
    }
}
