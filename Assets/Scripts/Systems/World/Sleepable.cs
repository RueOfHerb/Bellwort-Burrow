using UnityEngine;
using BellwortBurrow.Core;

namespace BellwortBurrow.Systems.World
{
    /// <summary>
    /// A place the player can sleep (sys-9): for now a Burrow home's door, since the homes have no insides yet.
    /// Interacting ends the day: the next one starts and the game saves. The prefab maker puts this on every Burrow home.
    /// </summary>
    public class Sleepable : MonoBehaviour, IInteractable
    {
        public Vector2 Position => transform.position;

        void OnEnable() => Interactables.Add(this);

        void OnDisable() => Interactables.Remove(this);

        public bool CanInteract(GameObject who) => ServiceLocator.TryGet<IGameSession>(out _);

        public void Interact(GameObject who)
        {
            if (ServiceLocator.TryGet<IGameSession>(out var session)) session.Sleep();
        }
    }
}
