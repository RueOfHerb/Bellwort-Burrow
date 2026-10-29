using UnityEditor;
using UnityEngine;
using BellwortBurrow.Data;
using BellwortBurrow.Systems.World;

namespace BellwortBurrow.EditorTools
{
    /// <summary>Gives a piece's prefab a Forageable when the Forage Table lists its slice (sys-8).</summary>
    public static class Foraging
    {
        public const string TablePath = "Assets/Data/ForageTable.asset";

        static ForageTable table;
        static bool looked;

        public static ForageTable Table
        {
            get
            {
                if (!looked)
                {
                    table = AssetDatabase.LoadAssetAtPath<ForageTable>(TablePath);
                    looked = true;
                }
                return table;
            }
        }

        /// <summary>Forget the cached table, for after it's made or changed.</summary>
        public static void Reset() => looked = false;

        public static bool Dress(GameObject root, string slice)
        {
            var row = Table != null ? Table.Find(slice) : null;
            if (row == null || row.item == null) return false;
            var forageable = root.GetComponent<Forageable>();
            if (forageable == null) forageable = root.AddComponent<Forageable>();
            forageable.Configure(row.item, row.amount, row.respawnDays, row.bonusItem, row.bonusAmount);
            return true;
        }
    }
}
