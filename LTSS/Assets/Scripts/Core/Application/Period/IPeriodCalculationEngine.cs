using System.Collections.Generic;
using Game.Domain.GameFlow;

namespace Game.Core.Application.Periods
{
    public interface IPeriodCalculationEngine
    {
        PeriodCalculationSummary Recalculate(
            PeriodRuntimeDefinition definition,
            IReadOnlyList<PeriodExpenseState> expenses,
            IReadOnlyList<PeriodAssetOperationEntry> assetOperations);
    }
}
