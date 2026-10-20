using System;
using System.Collections.Generic;

namespace BellwortBurrow.Data
{
    /// <summary>
    /// The choices behind each character creator row, and stepping through them (sys-6).
    /// The critter kind's and the face's choices come from the Critter Catalog; every other part's from Character Options.
    /// </summary>
    public static class AppearanceChoices
    {
        public const string NoChoice = "None yet";

        public static IReadOnlyList<AppearanceOption> For(AppearancePart part, CritterCatalog catalog, CharacterOptions options)
        {
            if (part == AppearancePart.Face && catalog != null && catalog.Faces.Count > 0)
            {
                var faces = new List<AppearanceOption>();
                foreach (var face in catalog.Faces)
                {
                    if (face != null) faces.Add(new AppearanceOption(face.id, face.DisplayName));
                }
                return faces;
            }
            if (part != AppearancePart.CritterKind)
                return options != null ? options.For(part) : Array.Empty<AppearanceOption>();

            var kinds = new List<AppearanceOption>();
            if (catalog == null) return kinds;
            foreach (var kind in catalog.Kinds)
            {
                if (kind != null) kinds.Add(new AppearanceOption(kind.Id, kind.DisplayName));
            }
            return kinds;
        }

        public static int IndexOf(IReadOnlyList<AppearanceOption> choices, string id)
        {
            if (choices == null) return -1;
            for (int i = 0; i < choices.Count; i++)
            {
                if (choices[i] != null && choices[i].id == id) return i;
            }
            return -1;
        }

        /// <summary>
        /// The id <paramref name="steps"/> places along from <paramref name="currentId"/>, looping around at either end.
        /// An id that isn't in the list counts as the first choice. With no choices, the id stays as it is.
        /// </summary>
        public static string Step(IReadOnlyList<AppearanceOption> choices, string currentId, int steps)
        {
            if (choices == null || choices.Count == 0) return currentId;

            int count = choices.Count;
            int index = Math.Max(0, IndexOf(choices, currentId));
            index = ((index + steps) % count + count) % count;
            return choices[index].id;
        }

        public static string DisplayName(IReadOnlyList<AppearanceOption> choices, string id)
        {
            int index = IndexOf(choices, id);
            return index >= 0 ? choices[index].displayName : NoChoice;
        }

        /// <summary>A new profile with the first choice of every part, and no name.</summary>
        public static PlayerProfile Defaults(CritterCatalog catalog, CharacterOptions options)
        {
            var profile = new PlayerProfile();
            foreach (AppearancePart part in Enum.GetValues(typeof(AppearancePart)))
            {
                var choices = For(part, catalog, options);
                if (choices.Count > 0) profile.Set(part, choices[0].id);
            }
            return profile;
        }
    }
}
