using System;
using UnityEngine;

namespace BellwortBurrow.Core
{
    public abstract class GameEventChannel<T> : ScriptableObject
    {
        public event Action<T> Raised;

        public void Raise(T payload) => Raised?.Invoke(payload);
    }

    [CreateAssetMenu(menuName = "Bellwort Burrow/Events/Void Event Channel", fileName = "NewVoidEventChannel")]
    public class VoidEventChannel : ScriptableObject
    {
        public event Action Raised;

        public void Raise() => Raised?.Invoke();
    }
}
