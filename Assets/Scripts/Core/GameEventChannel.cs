using System;
using UnityEngine;

namespace BellwortBurrow.Core
{
    public abstract class GameEventChannel<T> : ScriptableObject
    {
        public event Action<T> Raised;

        public void Raise(T payload) => Raised?.Invoke(payload);
    }
}
