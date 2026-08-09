using System;
using System.Collections.Generic;

namespace BellwortBurrow.Core
{
    public static class ServiceLocator
    {
        static readonly Dictionary<Type, object> services = new();

        public static void Register<TService>(TService service)
        {
            services[typeof(TService)] = service;
        }

        public static TService Get<TService>()
        {
            if (!services.TryGetValue(typeof(TService), out var service))
                throw new InvalidOperationException($"Service {typeof(TService).Name} is not registered.");

            return (TService)service;
        }

        public static bool TryGet<TService>(out TService service)
        {
            if (services.TryGetValue(typeof(TService), out var raw))
            {
                service = (TService)raw;
                return true;
            }

            service = default;
            return false;
        }

        public static void Clear() => services.Clear();

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        static void ClearOnDomainReload() => Clear();
#endif
    }
}
