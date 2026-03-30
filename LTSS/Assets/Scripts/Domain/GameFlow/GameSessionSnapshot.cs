namespace Game.Domain.GameFlow
{
    public sealed class GameSessionSnapshot
    {
        public static GameSessionSnapshot Empty =>
            new GameSessionSnapshot(
                string.Empty,
                false,
                GameFlowStage.None,
                GameFlowPhase.None,
                new GameMetricBag());

        public string SessionId { get; }
        public bool IsActive { get; }
        public GameFlowStage Stage { get; }
        public GameFlowPhase Phase { get; }
        public GameMetricBag Metrics { get; }

        public GameSessionSnapshot(
            string sessionId,
            bool isActive,
            GameFlowStage stage,
            GameFlowPhase phase,
            GameMetricBag metrics)
        {
            SessionId = sessionId ?? string.Empty;
            IsActive = isActive;
            Stage = stage;
            Phase = phase;
            Metrics = metrics ?? new GameMetricBag();
        }

        public GameSessionSnapshot With(
            string sessionId = null,
            bool? isActive = null,
            GameFlowStage? stage = null,
            GameFlowPhase? phase = null,
            GameMetricBag metrics = null)
        {
            return new GameSessionSnapshot(
                sessionId ?? SessionId,
                isActive ?? IsActive,
                stage ?? Stage,
                phase ?? Phase,
                metrics ?? Metrics.Clone());
        }
    }
}
