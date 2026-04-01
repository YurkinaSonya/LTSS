using Game.Core.Application.Session;
using Game.Domain.GameFlow;

namespace Game.Core.Application.Periods
{
    public interface IPeriodCheckpointBuilder
    {
        bool TryBuild(
            PeriodRuntimeState runtimeState,
            out CheckpointRequestDto request,
            out string error);
    }
}
