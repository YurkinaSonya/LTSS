namespace Game.Domain.GameFlow
{
    public enum GameFlowPhase
    {
        None,
        Idle,
        Preparation,
        LoadingData,
        PeriodIntro,
        PeriodActive,
        PeriodValidation,
        PeriodClosing,
        PeriodCheckpointSubmitting,
        PeriodClosed,
        Active,
        Paused,
        Completed,
        Summary
    }
}
