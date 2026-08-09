using UnityEngine;

namespace BellwortBurrow.Data
{
    [CreateAssetMenu(menuName = "Bellwort Burrow/Professions/Profession Definition", fileName = "NewProfession")]
    public class ProfessionDefinition : ScriptableObject
    {
        [SerializeField] string id;
        [SerializeField] string displayName;
        [SerializeField] Sprite icon;
        [SerializeField] AnimationCurve xpToLevelCurve = AnimationCurve.Linear(0, 100, 10, 1000);

        public string Id => id;
        public string DisplayName => displayName;
        public Sprite Icon => icon;

        public int XpRequiredForLevel(int level) => Mathf.RoundToInt(xpToLevelCurve.Evaluate(level));

        internal void ConfigureForTesting(AnimationCurve curve) => xpToLevelCurve = curve;
    }
}
