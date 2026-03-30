using System;
using Game.Core.Events;

namespace Game.Core.Application.State
{
    public sealed class ApplicationStateStore : IApplicationStateStore
    {
        private readonly IEventAggregator _eventAggregator;
        private ApplicationStateSnapshot _current = ApplicationStateSnapshot.Default;

        public ApplicationStateSnapshot Current => _current;

        public event Action<ApplicationStateSnapshot> StateChanged;

        public ApplicationStateStore(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
        }

        public void Reset()
        {
            _current = ApplicationStateSnapshot.Default;
            PublishState();
        }

        public void SetState(Func<ApplicationStateSnapshot, ApplicationStateSnapshot> mutator)
        {
            if (mutator == null)
            {
                return;
            }

            var nextState = mutator(_current) ?? _current;
            _current = nextState;
            PublishState();
        }

        private void PublishState()
        {
            StateChanged?.Invoke(_current);
            _eventAggregator.Publish(new EventsProvider.ApplicationStateChangedEvent(_current));
        }
    }
}
