using System;
using UnityEngine;

namespace BellwortBurrow.Core
{
    [CreateAssetMenu(menuName = "Bellwort Burrow/Events/Void Event Channel", fileName = "NewVoidEventChannel")]
    public class VoidEventChannel : ScriptableObject
    {
        public event Action Raised;

        public void Raise() => Raised?.Invoke();
    }
}
