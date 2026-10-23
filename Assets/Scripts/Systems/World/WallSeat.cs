using UnityEngine;

namespace BellwortBurrow.Systems.World
{
    /// <summary>
    /// A building whose walls take wall pieces (the great stump takes its ladder and lantern hook): a piece dropped on
    /// it becomes part of it and draws in front of it, inside its sorting group (see <see cref="WallPiece"/>).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WallSeat : MonoBehaviour
    {
    }
}
