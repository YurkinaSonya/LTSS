using System.Collections.Generic;
using Game.Core.Application.Logging;

namespace Game.Core.Application.State
{
    public sealed class ApplicationStateMachine
    {
        private readonly Dictionary<AppStateId, IApplicationState> _states;
        private readonly IAppLogger _logger;

        private IApplicationState _currentState;

        public AppStateId CurrentStateId => _currentState?.StateId ?? AppStateId.None;

        public ApplicationStateMachine(
            List<IApplicationState> states,
            IAppLogger logger)
        {
            _logger = logger;
            _states = new Dictionary<AppStateId, IApplicationState>();

            if (states == null)
            {
                return;
            }

            foreach (var state in states)
            {
                if (state == null || _states.ContainsKey(state.StateId))
                {
                    continue;
                }

                _states.Add(state.StateId, state);
            }
        }

        public void MoveTo(AppStateId stateId)
        {
            if (CurrentStateId == stateId)
            {
                return;
            }

            if (!_states.TryGetValue(stateId, out var nextState))
            {
                _logger.Error($"No application state is registered for '{stateId}'.");
                return;
            }

            _currentState?.Exit();
            _currentState = nextState;

            _logger.Info($"Application state changed to '{stateId}'.");
            _currentState.Enter();
        }
    }
}
