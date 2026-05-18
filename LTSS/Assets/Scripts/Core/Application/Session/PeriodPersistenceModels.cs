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
        public bool hasPersistedInitialBalances;
        public bool hasPersistedAccumulatedUje;
        public double initialCashBalance;
        public double initialDepositBalance;
        public double accumulatedUje;
        public bool hasPersistedCarryOverBalances;
        public bool hasPersistedCarryOverAccumulatedUje;
        public int carryOverTargetPeriodNumber;
        public double carryOverCashBalance;
        public double carryOverDepositBalance;
        public double carryOverAccumulatedUje;
        public ConsumerCreditContractSnapshotDto[] consumerCredits;
        public ResidenceOwnershipSnapshotDto residenceOwnership;
        public PensionReserveSnapshotDto pensionReserve;
        public PdsAccountSnapshotDto pdsAccount;
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

    [Serializable]
    public sealed class ConsumerCreditContractSnapshotDto
    {
        public string contractType;
        public string creditId;
        public int originationPeriodNumber;
        public double originalPrincipal;
        public double remainingPrincipal;
        public double periodicPayment;
        public double fixedRatePercent;
        public int remainingPeriods;
    }

    [Serializable]
    public sealed class ResidenceOwnershipSnapshotDto
    {
        public string residenceId;
        public double purchasePrice;
        public int purchasePeriodNumber;
        public double purchaseInflationMultiplier;
        public string acquisitionMode;
    }

    [Serializable]
    public sealed class PensionReserveSnapshotDto
    {
        public double balance;
        public bool isAccrualActive;
        public bool hasEverBeenActive;
    }

    [Serializable]
    public sealed class PdsAccountSnapshotDto
    {
        public string accountId;
        public double balance;
        public int activationPeriodNumber;
    }
}
