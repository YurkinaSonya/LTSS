using System.Collections.Generic;
using Zenject;

namespace Game.Core.Feature
{
    /// <summary>
    /// A lightweight entry point for application-level orchestration of domain subjects.
    /// </summary>
    public sealed class GameBootstrapper : IInitializable
    {
        private readonly List<IGameFeatureInitializable> _systems;

        public GameBootstrapper(List<IGameFeatureInitializable> systems)
        {
            _systems = systems;
        }

        /// <summary>
        /// Executes system initializers in the order provided by the container.
        /// </summary>
        public void Initialize()
        {
            if (_systems == null) return;

            foreach (var system in _systems)
                system?.Initialize();
        }
    }
}
