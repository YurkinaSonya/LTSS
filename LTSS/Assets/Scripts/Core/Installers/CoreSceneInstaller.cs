using UnityEngine;
using Zenject;
using Game.Core.Events;
using Game.Core.Feature;

namespace Game.Core.Installers
{
    /// <summary>
    /// Defines scene-scoped bindings for MonoBehaviours in scene.
    /// </summary>
    public sealed class CoreSceneInstaller : MonoInstaller
    {
        [Header("Demo / Presentation")]
        [SerializeField] private MonoBehaviour[] _sceneServices;

        public override void InstallBindings()
        {
            //Bootstrapper for initializable in-game objects
            Container.BindInterfacesAndSelfTo<GameBootstrapper>()
                .AsSingle()
                .NonLazy();

            //in-scene services
            if (_sceneServices == null) return;

            foreach (var service in _sceneServices)
            {
                if (service == null) continue;

                Container.BindInterfacesAndSelfTo(service.GetType())
                    .FromInstance(service)
                    .AsSingle();
            }
        }
    }
}
