using System.Collections.Generic;
using UnityEngine;

namespace BellwortBurrow.Systems.World
{
    /// <summary>Something the player can use with the Interact action when they stand near it (sys-8).</summary>
    public interface IInteractable
    {
        /// <summary>Where it is in the world, for picking the nearest one.</summary>
        Vector2 Position { get; }

        bool CanInteract(GameObject who);

        void Interact(GameObject who);
    }

    /// <summary>Every interactable in the loaded scenes, so the player can find the nearest one without physics.</summary>
    public static class Interactables
    {
        static readonly List<IInteractable> all = new();

        public static IReadOnlyList<IInteractable> All => all;

        public static void Add(IInteractable interactable)
        {
            if (!all.Contains(interactable)) all.Add(interactable);
        }

        public static void Remove(IInteractable interactable) => all.Remove(interactable);

        /// <summary>The nearest interactable to a point within reach that will take the interaction, or null.</summary>
        public static IInteractable Nearest(Vector2 at, float reach, GameObject who)
        {
            IInteractable best = null;
            float bestDistance = reach * reach;
            foreach (var candidate in all)
            {
                if (candidate == null || !candidate.CanInteract(who)) continue;
                float distance = (candidate.Position - at).sqrMagnitude;
                if (distance > bestDistance) continue;
                best = candidate;
                bestDistance = distance;
            }
            return best;
        }

        // Domain reload is off when entering Play mode, so the list would carry the last run's nodes.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ClearOnEnterPlayMode() => all.Clear();
    }
}
