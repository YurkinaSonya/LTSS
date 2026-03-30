using System;

namespace Game.Core.Application.Bootstrap
{
    [Serializable]
    public sealed class ApplicationBootstrapSettings
    {
        public AppStateId InitialState = AppStateId.MainMenu;
    }
}
