using System;
using System.Collections.Generic;
using UnityEngine;

namespace BellwortBurrow.Data
{
    /// <summary>
    /// Which sheet pieces can be picked, and what they give (sys-8). The prefab maker reads it when it makes a piece's
    /// prefab, so the art package stays plain art: add a row here and remake the prefabs to make a piece forageable.
    /// </summary>
    [CreateAssetMenu(menuName = "Bellwort Burrow/Items/Forage Table", fileName = "ForageTable")]
    public class ForageTable : ScriptableObject
    {
        [Serializable]
        public class Row
        {
            [Tooltip("The slice name on its sheet, like burrow_bellworts.")]
            public string slice;
            public ItemDefinition item;
            [Min(1)] public int amount = 1;
            [Min(1)] public int respawnDays = 1;
            [Tooltip("Something more it gives along with its item, like a Bellwort seed with the flower (sys-10). Optional.")]
            public ItemDefinition bonusItem;
            [Min(0)] public int bonusAmount = 1;
        }

        [SerializeField] List<Row> rows = new();

        public IReadOnlyList<Row> Rows => rows;

        public Row Find(string slice)
        {
            if (string.IsNullOrEmpty(slice)) return null;
            foreach (var row in rows)
                if (row != null && row.slice == slice) return row;
            return null;
        }

        internal void ConfigureForTesting(IEnumerable<Row> rows) => this.rows = new List<Row>(rows);
    }
}
