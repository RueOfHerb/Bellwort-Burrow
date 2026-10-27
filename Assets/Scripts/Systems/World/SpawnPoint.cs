using UnityEngine;

namespace BellwortBurrow.Systems.World
{
    /// <summary>Where the player arrives in a zone through a <see cref="ZoneExit"/> (lvl-4). Stands a step inside the zone, clear of the exit's box.</summary>
    public sealed class SpawnPoint : MonoBehaviour
    {
        public string id = "";

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.9f, 0.75f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, 0.4f);
        }
    }
}
