using System;

namespace Game.Core.Application.Session
{
    [Serializable]
    public sealed class PersistedPeriodSnapshot
    {
        public string runId;
        public int periodNumber;
        public string flowState;
        public string rawPeriodStateJson;
        public string savedAtUtc;
        public bool isCheckpointSubmitted;
        public bool canRestore;
    }

    [Serializable]
    public sealed class PeriodRuntimeSnapshotDto
    {
        public string runId;
        public int periodNumber;
        public string flowState;
        public PeriodExpenseStateSnapshotDto[] expenses;
        public PeriodAssetOperationSnapshotDto[] assetOperations;
        public bool isCheckpointSubmitted;
        public string statusMessage;
        public string lastError;
        public string submittedAtUtc;
        public bool hasPendingLocalChanges;
    }

    [Serializable]
    public sealed class PeriodExpenseStateSnapshotDto
    {
        public string expenseId;
        public double amount;
        public string source;
    }

    [Serializable]
    public sealed class PeriodAssetOperationSnapshotDto
    {
        public string operationId;
        public string assetId;
        public string kind;
        public string source;
        public double amount;
        public string createdAtUtc;
    }
}
