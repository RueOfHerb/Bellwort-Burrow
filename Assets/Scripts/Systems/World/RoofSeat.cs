using UnityEngine;

namespace BellwortBurrow.Systems.World
{
    /// <summary>
    /// A house that can take a roof on top: its size says which roofs fit (burrow_nook, burrow_home, burrow_home_wide).
    /// A roof that fits sits on the house's own spot, since a roof's pivot is the house's feet (see <see cref="HouseRoof"/>).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoofSeat : MonoBehaviour
    {
        [Tooltip("Which roofs fit this house: its size, like burrow_home.")]
        public string size;
    }
}
