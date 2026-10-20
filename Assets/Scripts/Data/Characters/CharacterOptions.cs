using System;
using System.Collections.Generic;
using UnityEngine;

namespace BellwortBurrow.Data
{
    /// <summary>One choice in a character creator row.</summary>
    [Serializable]
    public class AppearanceOption
    {
        [Tooltip("Saved in the player profile. Never change it once saves exist.")]
        public string id;
        public string displayName;

        public AppearanceOption() { }

        public AppearanceOption(string id, string displayName)
        {
            this.id = id;
            this.displayName = displayName;
        }
    }

    /// <summary>The choices for one appearance part, in the order the creator shows them.</summary>
    [Serializable]
    public class AppearanceOptionList
    {
        public AppearancePart part;
        public List<AppearanceOption> options = new();
    }

    /// <summary>
    /// The choices for every appearance part except the critter kind, which comes from the Critter Catalog (sys-6).
    /// Adding a choice here is all it takes to show it in the character creator; drawing it on the player comes with the art.
    /// Color choices are ramps from characters.gpl, by name.
    /// </summary>
    [CreateAssetMenu(menuName = "Bellwort Burrow/Characters/Character Options", fileName = "CharacterOptions")]
    public class CharacterOptions : ScriptableObject
    {
        [SerializeField] List<AppearanceOptionList> parts = new();

        public IReadOnlyList<AppearanceOption> For(AppearancePart part)
        {
            foreach (var list in parts)
            {
                if (list != null && list.part == part) return list.options;
            }
            return Array.Empty<AppearanceOption>();
        }

        internal void ConfigureForTesting(IEnumerable<AppearanceOptionList> parts) => this.parts = new List<AppearanceOptionList>(parts);
    }
}
