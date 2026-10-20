using System;
using UnityEngine;

namespace BellwortBurrow.Data
{
    /// <summary>
    /// One face the player can pick (sys-6): eyes and brows, plus a mouth. Faces are neutral and differ in their features,
    /// and every critter has the same head, so each face is drawn once and fits every kind. They live in
    /// Assets/Art/Characters/Faces/player_faces.aseprite, one slice per part, each a whole 24 by 48 player frame.
    /// </summary>
    [Serializable]
    public class FaceStyle
    {
        [Tooltip("Saved in the player profile. Never change it once saves exist.")]
        public string id;
        public string displayName;
        [Tooltip("The eyes and brows: a face_<name> slice from player_faces.aseprite.")]
        public Sprite eyes;
        [Tooltip("The mouth: the face_<name>_mouth slice. A kind with a beak doesn't show it.")]
        public Sprite mouth;

        public FaceStyle() { }

        public FaceStyle(string id, string displayName, Sprite eyes, Sprite mouth)
        {
            this.id = id;
            this.displayName = displayName;
            this.eyes = eyes;
            this.mouth = mouth;
        }

        public string DisplayName => string.IsNullOrEmpty(displayName) ? id : displayName;
    }
}
