using System;
using System.Collections.Generic;

namespace AdaptiveBossArena.Core.Services
{
    /// <summary>
    /// Minimal interface-keyed container that the composition root fills at scene start.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Deliberately not a full dependency-injection framework. The project has perhaps a dozen
    /// long-lived services, and a container that resolves constructors and manages lifetimes would
    /// add far more machinery than that justifies.
    /// </para>
    /// <para>
    /// Prefer constructor or inspector injection wherever a dependency can be handed over directly.
    /// Reach for the registry only where Unity's object model gets in the way, most commonly a
    /// component that Unity instantiates and therefore cannot receive constructor arguments.
    /// </para>
    /// </remarks>
    public sealed class ServiceRegistry
    {
        private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

        /// <summary>
        /// The registry the running scene is using.
        /// </summary>
        /// <remarks>
        /// The single global access point in the project, and intentionally the only one. Anything
        /// reaching for it should be doing so because Unity instantiated it and no seam existed to
        /// pass dependencies through.
        /// </remarks>
        public static ServiceRegistry Current { get; private set; } = new ServiceRegistry();

        /// <summary>Installs a registry as the current one, replacing any predecessor.</summary>
        /// <param name="registry">The registry to install.</param>
        /// <exception cref="ArgumentNullException">Thrown when no registry is supplied.</exception>
        public static void SetCurrent(ServiceRegistry registry) =>
            Current = registry ?? throw new ArgumentNullException(nameof(registry));

        /// <summary>Number of services currently registered.</summary>
        public int Count => _services.Count;

        /// <summary>Registers an implementation against a service interface.</summary>
        /// <typeparam name="TService">The interface consumers will ask for.</typeparam>
        /// <param name="implementation">The instance to hand out.</param>
        /// <exception cref="ArgumentNullException">Thrown when no implementation is supplied.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the service is already registered.</exception>
        public void Register<TService>(TService implementation) where TService : class
        {
            if (implementation == null)
            {
                throw new ArgumentNullException(nameof(implementation));
            }

            Type key = typeof(TService);
            if (_services.ContainsKey(key))
            {
                // Silently replacing would let a duplicated manager in a scene mask itself and
                // produce a bug that only appears after a scene reload.
                throw new InvalidOperationException(
                    $"Service '{key.Name}' is already registered. Unregister it first if replacement is intended.");
            }

            _services.Add(key, implementation);
        }

        /// <summary>Registers a service, replacing any existing registration.</summary>
        /// <typeparam name="TService">The interface consumers will ask for.</typeparam>
        /// <param name="implementation">The instance to hand out.</param>
        public void RegisterOrReplace<TService>(TService implementation) where TService : class
        {
            if (implementation == null)
            {
                throw new ArgumentNullException(nameof(implementation));
            }

            _services[typeof(TService)] = implementation;
        }

        /// <summary>Resolves a registered service.</summary>
        /// <typeparam name="TService">The interface to resolve.</typeparam>
        /// <returns>The registered implementation.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the service was never registered.</exception>
        public TService Get<TService>() where TService : class
        {
            if (_services.TryGetValue(typeof(TService), out object service))
            {
                return (TService)service;
            }

            throw new InvalidOperationException(
                $"Service '{typeof(TService).Name}' was not registered. " +
                "Check that the composition root runs before the consumer's initialisation.");
        }

        /// <summary>Attempts to resolve a service without throwing when it is absent.</summary>
        /// <typeparam name="TService">The interface to resolve.</typeparam>
        /// <param name="service">Receives the implementation, or null.</param>
        /// <returns>True when the service was registered.</returns>
        public bool TryGet<TService>(out TService service) where TService : class
        {
            if (_services.TryGetValue(typeof(TService), out object found))
            {
                service = (TService)found;
                return true;
            }

            service = null;
            return false;
        }

        /// <summary>Removes a registration.</summary>
        /// <typeparam name="TService">The interface to unregister.</typeparam>
        /// <returns>True when a registration was removed.</returns>
        public bool Unregister<TService>() where TService : class => _services.Remove(typeof(TService));

        /// <summary>Removes every registration.</summary>
        public void Clear() => _services.Clear();
    }
}
