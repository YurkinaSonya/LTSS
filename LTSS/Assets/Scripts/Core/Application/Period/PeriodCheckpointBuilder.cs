using System;
using System.Collections.Generic;
using Game.Core.Application.Networking;
using Game.Core.Application.Session;
using Game.Domain.GameFlow;

namespace Game.Core.Application.Periods
{
    [Serializable]
    public sealed class PeriodCheckpointEnvelopeDto
    {
        public int periodNumber;
        public string periodId;
        public string title;
        public string historicalLabel;
        public double disposableIncome;
        public double totalExpenses;
        public double remainingToAllocate;
        public double uje;
        public PeriodCheckpointExpenseDto[] expenses;
        public PeriodCheckpointAssetDto[] assets;
        public PeriodCheckpointAssetOperationDto[] assetOperations;
        public PeriodCheckpointTechnicalDto technical;
    }

    [Serializable]
    public sealed class PeriodCheckpointExpenseDto
    {
        public string expenseId;
        public string title;
        public double amount;
        public string source;
        public bool required;
    }

    [Serializable]
    public sealed class PeriodCheckpointAssetDto
    {
        public string assetId;
        public string title;
        public double initialAmount;
        public double currentAmount;
    }

    [Serializable]
    public sealed class PeriodCheckpointAssetOperationDto
    {
        public string operationId;
        public string assetId;
        public string kind;
        public string source;
        public double amount;
        public string createdAtUtc;
    }

    [Serializable]
    public sealed class PeriodCheckpointTechnicalDto
    {
        public string flowState;
        public string sourceSummary;
        public string statusMessage;
        public string lastError;
        public bool canComplete;
    }

    [Serializable]
    public sealed class PeriodCheckpointSummaryDto
    {
        public int periodNumber;
        public double totalExpenses;
        public double cashBalance;
        public double depositBalance;
        public double remainingToAllocate;
        public double uje;
        public int blockingIssueCount;
    }

    public sealed class PeriodCheckpointBuilder : IPeriodCheckpointBuilder
    {
        private readonly IJsonSerializer _serializer;

        public PeriodCheckpointBuilder(IJsonSerializer serializer)
        {
            _serializer = serializer;
        }

        public bool TryBuild(
            PeriodRuntimeState runtimeState,
            out CheckpointRequestDto request,
            out string error)
        {
            request = null;
            error = string.Empty;

            if (runtimeState == null || !runtimeState.HasDefinition)
            {
                error = "Состояние периода отсутствует.";
                return false;
            }

            var summary = runtimeState.Summary ?? PeriodCalculationSummary.Empty;

            if (!summary.CanComplete)
            {
                error = "Период нельзя завершить в невалидном состоянии.";
                return false;
            }

            var expenses = new List<PeriodCheckpointExpenseDto>(runtimeState.Expenses.Count);

            foreach (var expenseState in runtimeState.Expenses)
            {
                if (expenseState == null)
                {
                    continue;
                }

                var definition = FindExpenseDefinition(runtimeState.Definition, expenseState.ExpenseId);

                expenses.Add(new PeriodCheckpointExpenseDto
                {
                    expenseId = expenseState.ExpenseId,
                    title = definition != null ? definition.Title : expenseState.ExpenseId,
                    amount = expenseState.Amount,
                    source = PeriodContractMapper.ToFundsSourceCode(expenseState.Source),
                    required = definition != null && definition.IsRequired
                });
            }

            var assets = new List<PeriodCheckpointAssetDto>(summary.AssetBalances.Count);

            foreach (var asset in summary.AssetBalances)
            {
                if (asset == null)
                {
                    continue;
                }

                assets.Add(new PeriodCheckpointAssetDto
                {
                    assetId = asset.AssetId,
                    title = asset.Title,
                    initialAmount = asset.InitialAmount,
                    currentAmount = asset.CurrentAmount
                });
            }

            var operations = new List<PeriodCheckpointAssetOperationDto>(runtimeState.AssetOperations.Count);

            foreach (var operation in runtimeState.AssetOperations)
            {
                if (operation == null)
                {
                    continue;
                }

                operations.Add(new PeriodCheckpointAssetOperationDto
                {
                    operationId = operation.OperationId,
                    assetId = operation.AssetId,
                    kind = PeriodContractMapper.ToAssetOperationKindCode(operation.Kind),
                    source = PeriodContractMapper.ToFundsSourceCode(operation.Source),
                    amount = operation.Amount,
                    createdAtUtc = operation.CreatedAtUtc
                });
            }

            var blockingIssueCount = 0;

            foreach (var issue in summary.ValidationIssues)
            {
                if (issue != null && issue.IsBlocking)
                {
                    blockingIssueCount++;
                }
            }

            var checkpointEnvelope = new PeriodCheckpointEnvelopeDto
            {
                periodNumber = runtimeState.PeriodNumber,
                periodId = runtimeState.Definition.Meta.PeriodId,
                title = runtimeState.Definition.Meta.Title,
                historicalLabel = runtimeState.Definition.Meta.HistoricalLabel,
                disposableIncome = summary.DisposableIncome,
                totalExpenses = summary.TotalExpenses,
                remainingToAllocate = summary.RemainingToAllocate,
                uje = summary.Uje,
                expenses = expenses.ToArray(),
                assets = assets.ToArray(),
                assetOperations = operations.ToArray(),
                technical = new PeriodCheckpointTechnicalDto
                {
                    flowState = PeriodContractMapper.ToPeriodFlowStateCode(runtimeState.FlowState),
                    sourceSummary = runtimeState.Definition.SourceSummary,
                    statusMessage = runtimeState.StatusMessage,
                    lastError = runtimeState.LastError,
                    canComplete = summary.CanComplete
                }
            };

            var summaryPayload = new PeriodCheckpointSummaryDto
            {
                periodNumber = runtimeState.PeriodNumber,
                totalExpenses = summary.TotalExpenses,
                cashBalance = summary.CashBalance,
                depositBalance = summary.DepositBalance,
                remainingToAllocate = summary.RemainingToAllocate,
                uje = summary.Uje,
                blockingIssueCount = blockingIssueCount
            };

            request = new CheckpointRequestDto
            {
                periodNumber = runtimeState.PeriodNumber,
                checkpointJson = _serializer.Serialize(checkpointEnvelope),
                summaryJson = _serializer.Serialize(summaryPayload),
                clientTimestampUtc = DateTime.UtcNow.ToString("O")
            };

            return true;
        }

        private static PeriodExpenseDefinition FindExpenseDefinition(
            PeriodRuntimeDefinition definition,
            string expenseId)
        {
            if (definition == null || definition.ExpenseDefinitions == null)
            {
                return null;
            }

            foreach (var expenseDefinition in definition.ExpenseDefinitions)
            {
                if (expenseDefinition != null
                    && string.Equals(expenseDefinition.Id, expenseId, StringComparison.Ordinal))
                {
                    return expenseDefinition;
                }
            }

            return null;
        }
    }
}
