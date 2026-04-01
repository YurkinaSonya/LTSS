using System;
using Game.Domain.GameFlow;

namespace Game.Core.Application.State
{
    public interface IGameSessionService
    {
        GameSessionSnapshot Current { get; }
        event Action<GameSessionSnapshot> Changed;

        void LoadSession(GameSessionSnapshot snapshot);
        void ResetSession();
        void StartNewSession();
        void SetStage(GameFlowStage stage);
        void SetPhase(GameFlowPhase phase);
        void CompleteSession();
        void RecordMetric(string key, double value);
        void IncrementMetric(string key, double delta = 1d);
        void RecordTextMetric(string key, string value);
    }
}
