using Game.Core.Application.Session;
using Game.Domain.GameFlow;

namespace Game.Core.Application.Periods
{
    public interface IPeriodRuntimeFactory
    {
        bool TryCreateNew(
            ClientRuntimeState clientRuntime,
            out PeriodRuntimeState runtimeState,
            out string error);

        bool TryRestore(
            ClientRuntimeState clientRuntime,
            PersistedPeriodSnapshot snapshot,
            out PeriodRuntimeState runtimeState,
            out string error);
    }
}
