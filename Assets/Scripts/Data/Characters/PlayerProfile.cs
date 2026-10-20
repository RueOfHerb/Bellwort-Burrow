using System;

namespace BellwortBurrow.Data
{
    /// <summary>The parts of the player that a character creator row changes.</summary>
    public enum AppearancePart
    {
        CritterKind,
        FurColor,
        MarkingsColor,
        EyeColor,
        Face,
        Shirt,
        Trousers,
        Belt
    }

    /// <summary>
    /// Who the player is: a name and one option id per appearance part (sys-6).
    /// Ids only, never assets, so it saves as plain JSON. Every player is Critterfolk, so there's no race.
    /// </summary>
    [Serializable]
    public class PlayerProfile
    {
        public string name = "";
        public string critterKind = "";
        public string furColor = "";
        public string markingsColor = "";
        public string eyeColor = "";
        public string face = "";
        public string shirt = "";
        public string trousers = "";
        public string belt = "";

        public string Get(AppearancePart part) => part switch
        {
            AppearancePart.CritterKind => critterKind,
            AppearancePart.FurColor => furColor,
            AppearancePart.MarkingsColor => markingsColor,
            AppearancePart.EyeColor => eyeColor,
            AppearancePart.Face => face,
            AppearancePart.Shirt => shirt,
            AppearancePart.Trousers => trousers,
            AppearancePart.Belt => belt,
            _ => throw new ArgumentOutOfRangeException(nameof(part), part, null)
        };

        public void Set(AppearancePart part, string id)
        {
            id ??= "";
            switch (part)
            {
                case AppearancePart.CritterKind: critterKind = id; break;
                case AppearancePart.FurColor: furColor = id; break;
                case AppearancePart.MarkingsColor: markingsColor = id; break;
                case AppearancePart.EyeColor: eyeColor = id; break;
                case AppearancePart.Face: face = id; break;
                case AppearancePart.Shirt: shirt = id; break;
                case AppearancePart.Trousers: trousers = id; break;
                case AppearancePart.Belt: belt = id; break;
                default: throw new ArgumentOutOfRangeException(nameof(part), part, null);
            }
        }

        public PlayerProfile Clone() => (PlayerProfile)MemberwiseClone();
    }
}
