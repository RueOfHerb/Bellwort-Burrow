using System;
using UnityEngine;

namespace BellwortBurrow.Data
{
    /// <summary>
    /// The parts of the player that a character creator row changes. The numbers are saved in Character Options, so
    /// new parts go at the end. Top, Bottom and Waist were Shirt, Trousers and Belt.
    /// </summary>
    public enum AppearancePart
    {
        CritterKind = 0,
        FurColor = 1,
        MarkingsColor = 2,
        EyeColor = 3,
        Face = 4,
        Top = 5,
        Bottom = 6,
        Waist = 7,
        TopColor = 8,
        BottomColor = 9,
        Neck = 10,
        Ears = 11
    }

    /// <summary>
    /// Who the player is: a name and one option id per appearance part (sys-6).
    /// Ids only, never assets, so it saves as plain JSON. Every player is Critterfolk, so there's no race.
    /// </summary>
    [Serializable]
    public class PlayerProfile : ISerializationCallbackReceiver
    {
        public string name = "";
        public string critterKind = "";
        public string furColor = "";
        public string markingsColor = "";
        public string eyeColor = "";
        public string face = "";
        public string top = "";
        public string topColor = "";
        public string bottom = "";
        public string bottomColor = "";
        public string neck = "";
        public string waist = "";
        public string ears = "";

        // Saves made before clothes had slots call them shirt, trousers and belt. JsonUtility ignores
        // FormerlySerializedAs, so those names are read here and moved over once the save has loaded.
        [SerializeField, HideInInspector] string shirt;
        [SerializeField, HideInInspector] string trousers;
        [SerializeField, HideInInspector] string belt;

        public string Get(AppearancePart part) => part switch
        {
            AppearancePart.CritterKind => critterKind,
            AppearancePart.FurColor => furColor,
            AppearancePart.MarkingsColor => markingsColor,
            AppearancePart.EyeColor => eyeColor,
            AppearancePart.Face => face,
            AppearancePart.Top => top,
            AppearancePart.TopColor => topColor,
            AppearancePart.Bottom => bottom,
            AppearancePart.BottomColor => bottomColor,
            AppearancePart.Neck => neck,
            AppearancePart.Waist => waist,
            AppearancePart.Ears => ears,
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
                case AppearancePart.Top: top = id; break;
                case AppearancePart.TopColor: topColor = id; break;
                case AppearancePart.Bottom: bottom = id; break;
                case AppearancePart.BottomColor: bottomColor = id; break;
                case AppearancePart.Neck: neck = id; break;
                case AppearancePart.Waist: waist = id; break;
                case AppearancePart.Ears: ears = id; break;
                default: throw new ArgumentOutOfRangeException(nameof(part), part, null);
            }
        }

        public PlayerProfile Clone() => (PlayerProfile)MemberwiseClone();

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            shirt = trousers = belt = null;
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            if (string.IsNullOrEmpty(top) && !string.IsNullOrEmpty(shirt)) top = shirt;
            if (string.IsNullOrEmpty(bottom) && !string.IsNullOrEmpty(trousers)) bottom = trousers;
            if (string.IsNullOrEmpty(waist) && !string.IsNullOrEmpty(belt)) waist = belt;
            shirt = trousers = belt = null;
        }
    }
}
