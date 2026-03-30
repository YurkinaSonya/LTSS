using System;
using System.Collections.Generic;
using Game.Core.Application.Logging;
using Game.Core.Events;
using Game.Domain.GameFlow;

namespace Game.Core.Application.State
{
    public sealed class GameSessionService : IGameSessionService
    {
        private readonly IApplicationStateStore _stateStore;
        private readonly IEventAggregator _eventAggregator;
        private readonly IUserActionLogger _userActionLogger;

        private GameSessionSnapshot _current = GameSessionSnapshot.Empty;

        public GameSessionSnapshot Current => _current;

        public event Action<GameSessionSnapshot> Changed;

        public GameSessionService(
            IApplicationStateStore stateStore,
            IEventAggregator eventAggregator,
            IUserActionLogger userActionLogger)
        {
            _stateStore = stateStore;
            _eventAggregator = eventAggregator;
            _userActionLogger = userActionLogger;
        }

        public void ResetSession()
        {
            _current = GameSessionSnapshot.Empty;
            Publish();
        }

        public void StartNewSession()
        {
            _current = new GameSessionSnapshot(
                Guid.NewGuid().ToString("N"),
                true,
                GameFlowStage.Gameplay,
                GameFlowPhase.Preparation,
                new GameMetricBag());

            _userActionLogger.Log(
                UserActionType.SessionLifecycle,
                "session_started",
                new Dictionary<string, string>
                {
                    { "sessionId", _current.SessionId }
                });

            Publish();
        }

        public void SetStage(GameFlowStage stage)
        {
            _current = _current.With(stage: stage);
            Publish();
        }

        public void SetPhase(GameFlowPhase phase)
        {
            _current = _current.With(phase: phase);
            Publish();
        }

        public void CompleteSession()
        {
            _current = _current.With(
                isActive: false,
                stage: GameFlowStage.Results,
                phase: GameFlowPhase.Summary);

            if (!string.IsNullOrEmpty(_current.SessionId))
            {
                _userActionLogger.Log(
                    UserActionType.SessionLifecycle,
                    "session_completed",
                    new Dictionary<string, string>
                    {
                        { "sessionId", _current.SessionId }
                    });
            }

            Publish();
        }

        public void RecordMetric(string key, double value)
        {
            var metrics = _current.Metrics.Clone();
            metrics.SetNumber(key, value);
            _current = _current.With(metrics: metrics);
            Publish();
        }

        public void IncrementMetric(string key, double delta = 1d)
        {
            var metrics = _current.Metrics.Clone();
            metrics.Increment(key, delta);
            _current = _current.With(metrics: metrics);
            Publish();
        }

        public void RecordTextMetric(string key, string value)
        {
            var metrics = _current.Metrics.Clone();
            metrics.SetText(key, value);
            _current = _current.With(metrics: metrics);
            Publish();
        }

        private void Publish()
        {
            _stateStore.SetState(state => state.With(
                gameFlowStage: _current.Stage,
                gameFlowPhase: _current.Phase));

            Changed?.Invoke(_current);
            _eventAggregator.Publish(new EventsProvider.GameSessionChangedEvent(_current));
        }
    }
}
