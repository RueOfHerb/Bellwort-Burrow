using System.Text.RegularExpressions;
using UnityEngine;
using BellwortBurrow.Systems.World;

namespace BellwortBurrow.EditorTools
{
    /// <summary>Which pieces the player can sleep at (sys-9): the Burrow homes, not their roofs.</summary>
    public static class Sleeping
    {
        static readonly Regex Home = new Regex(@"^burrow_(nook|home|home_wide)_[a-z]+$");

        public static bool IsHome(string slice) => slice != null && Home.IsMatch(slice);

        public static bool Dress(GameObject root, string slice)
        {
            if (!IsHome(slice)) return false;
            if (root.GetComponent<Sleepable>() == null) root.AddComponent<Sleepable>();
            return true;
        }
    }
}
