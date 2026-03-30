using System;

namespace Game.Core.Application.State
{
    public interface IApplicationStateStore
    {
        ApplicationStateSnapshot Current { get; }
        event Action<ApplicationStateSnapshot> StateChanged;

        void Reset();
        void SetState(Func<ApplicationStateSnapshot, ApplicationStateSnapshot> mutator);
    }
}
