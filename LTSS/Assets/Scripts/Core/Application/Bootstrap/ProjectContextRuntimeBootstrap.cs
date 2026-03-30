using UnityEngine;
using Zenject;

namespace Game.Core.Application.Bootstrap
{
    public static class ProjectContextRuntimeBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureProjectContext()
        {
            if (!UnityEngine.Application.isPlaying)
            {
                return;
            }

            ProjectContext.Instance.EnsureIsInitialized();
        }
    }
}
