using NUnit.Framework;
using UnityEngine;
using BellwortBurrow.Data;
using BellwortBurrow.Systems.Professions;

namespace BellwortBurrow.Tests.EditMode
{
    public class ProfessionServiceTests
    {
        [Test]
        public void AddXp_CrossingLevelThreshold_RaisesLeveledUpOnce()
        {
            var profession = ScriptableObject.CreateInstance<ProfessionDefinition>();
            profession.ConfigureForTesting(AnimationCurve.Constant(0, 100, 50));

            var service = new ProfessionService();
            int leveledUpCount = 0;
            int leveledLevel = 0;
            service.OnProfessionLeveledUp += (_, level) =>
            {
                leveledUpCount++;
                leveledLevel = level;
            };

            service.AddXp(profession, 60);

            Assert.AreEqual(1, leveledUpCount);
            Assert.AreEqual(2, leveledLevel);
            Assert.AreEqual(2, service.GetState(profession).Level);
            Assert.AreEqual(10, service.GetState(profession).CurrentXp);
        }
    }
}
