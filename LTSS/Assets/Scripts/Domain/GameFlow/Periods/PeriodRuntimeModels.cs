using System;
using System.Collections.Generic;
using System.Globalization;

namespace Game.Domain.GameFlow
{
    public enum PeriodFlowState
    {
        None,
        LoadingData,
        PeriodIntro,
        PeriodActive,
        PeriodValidation,
        PeriodClosing,
        PeriodCheckpointSubmitting,
        PeriodClosed
    }

    public enum FundsSourceType
    {
        Unknown,
        CurrentIncome,
        Cash,
        Deposit
    }

    public enum PeriodAssetType
    {
        Generic,
        Cash,
        Deposit
    }

    public enum AssetOperationKind
    {
        None,
        Deposit,
        Withdraw
    }

    public sealed class PeriodMeta
    {
        public string PeriodId { get; }
        public int PeriodNumber { get; }
        public string Title { get; }
        public string HistoricalLabel { get; }
        public string Summary { get; }

        public PeriodMeta(
            string periodId,
            int periodNumber,
            string title,
            string historicalLabel,
            string summary)
        {
            PeriodId = periodId ?? string.Empty;
            PeriodNumber = periodNumber;
            Title = title ?? string.Empty;
            HistoricalLabel = historicalLabel ?? string.Empty;
            Summary = summary ?? string.Empty;
        }
    }

    public sealed class PeriodInfoBlockValue
    {
        public string Id { get; }
        public string Label { get; }
        public double? NumericValue { get; }
        public string Suffix { get; }
        public string RawText { get; }
        public bool HasValue => NumericValue.HasValue || !string.IsNullOrWhiteSpace(RawText);
        public string DisplayValue
        {
            get
            {
                if (NumericValue.HasValue)
                {
                    var formatted = NumericValue.Value.ToString("0.###", CultureInfo.InvariantCulture);
                    return string.IsNullOrWhiteSpace(Suffix)
                        ? formatted
                        : $"{formatted}{Suffix}";
                }

                return RawText ?? string.Empty;
            }
        }

        public PeriodInfoBlockValue(
            string id,
            string label,
            double? numericValue,
            string suffix,
            string rawText)
        {
            Id = id ?? string.Empty;
            Label = label ?? string.Empty;
            NumericValue = numericValue;
            Suffix = suffix ?? string.Empty;
            RawText = rawText ?? string.Empty;
        }
    }

    public sealed class PeriodEconomyContext
    {
        public static PeriodEconomyContext Empty { get; } = new PeriodEconomyContext(
            0,
            string.Empty,
            null,
            null,
            null,
            null,
            null,
            100d,
            100d,
            1d,
            1d,
            1d,
            1d);

        public int CurrentPeriodNumber { get; }
        public string HistoricalYear { get; }
        public double? NominalIncomeGrowth { get; }
        public double? Inflation { get; }
        public double? DepositRate { get; }
        public double? CreditRate { get; }
        public double? MortgageRate { get; }
        public double BaseIncomeEcu { get; }
        public double CurrentIncomeEcu { get; }
        public double ExpenseInflationMultiplier { get; }
        public double CurrentInflationMultiplier { get; }
        public double CashValueMultiplier { get; }
        public double DepositValueMultiplier { get; }

        public PeriodEconomyContext(
            int currentPeriodNumber,
            string historicalYear,
            double? nominalIncomeGrowth,
            double? inflation,
            double? depositRate,
            double? creditRate,
            double? mortgageRate,
            double baseIncomeEcu,
            double currentIncomeEcu,
            double expenseInflationMultiplier,
            double currentInflationMultiplier,
            double cashValueMultiplier,
            double depositValueMultiplier)
        {
            CurrentPeriodNumber = currentPeriodNumber;
            HistoricalYear = historicalYear ?? string.Empty;
            NominalIncomeGrowth = nominalIncomeGrowth;
            Inflation = inflation;
            DepositRate = depositRate;
            CreditRate = creditRate;
            MortgageRate = mortgageRate;
            BaseIncomeEcu = baseIncomeEcu;
            CurrentIncomeEcu = currentIncomeEcu;
            ExpenseInflationMultiplier = expenseInflationMultiplier;
            CurrentInflationMultiplier = currentInflationMultiplier;
            CashValueMultiplier = cashValueMultiplier;
            DepositValueMultiplier = depositValueMultiplier;
        }
    }

    public sealed class PeriodExpenseDefinition
    {
        public string Id { get; }
        public string Title { get; }
        public bool IsRequired { get; }
        public double DefaultAmount { get; }
        public double MinimumAmount { get; }
        public double MaximumAmount { get; }
        public IReadOnlyList<FundsSourceType> AllowedSources { get; }
        public double UjeWeight { get; }
        public double UjeReferenceAmount { get; }
        public string Description { get; }

        public PeriodExpenseDefinition(
            string id,
            string title,
            bool isRequired,
            double defaultAmount,
            double minimumAmount,
            double maximumAmount,
            IReadOnlyList<FundsSourceType> allowedSources,
            double ujeWeight,
            double ujeReferenceAmount,
            string description)
        {
            Id = id ?? string.Empty;
            Title = title ?? string.Empty;
            IsRequired = isRequired;
            DefaultAmount = defaultAmount;
            MinimumAmount = minimumAmount;
            MaximumAmount = maximumAmount;
            AllowedSources = allowedSources != null
                ? new List<FundsSourceType>(allowedSources)
                : Array.Empty<FundsSourceType>();
            UjeWeight = ujeWeight;
            UjeReferenceAmount = ujeReferenceAmount;
            Description = description ?? string.Empty;
        }
    }

    public sealed class PeriodAssetDefinition
    {
        public string Id { get; }
        public string Title { get; }
        public PeriodAssetType AssetType { get; }
        public bool AllowsDeposit { get; }
        public bool AllowsWithdraw { get; }
        public IReadOnlyList<FundsSourceType> AllowedDepositSources { get; }
        public string Description { get; }

        public PeriodAssetDefinition(
            string id,
            string title,
            PeriodAssetType assetType,
            bool allowsDeposit,
            bool allowsWithdraw,
            IReadOnlyList<FundsSourceType> allowedDepositSources,
            string description)
        {
            Id = id ?? string.Empty;
            Title = title ?? string.Empty;
            AssetType = assetType;
            AllowsDeposit = allowsDeposit;
            AllowsWithdraw = allowsWithdraw;
            AllowedDepositSources = allowedDepositSources != null
                ? new List<FundsSourceType>(allowedDepositSources)
                : Array.Empty<FundsSourceType>();
            Description = description ?? string.Empty;
        }
    }

    public sealed class PeriodValidationSettings
    {
        public bool RequireAllIncomeAllocated { get; }
        public bool RequireRequiredExpenses { get; }
        public bool PreventNegativeAssetBalances { get; }
        public double CompletionRemainderTolerance { get; }
        public double MinimumUjeToComplete { get; }

        public PeriodValidationSettings(
            bool requireAllIncomeAllocated,
            bool requireRequiredExpenses,
            bool preventNegativeAssetBalances,
            double completionRemainderTolerance,
            double minimumUjeToComplete)
        {
            RequireAllIncomeAllocated = requireAllIncomeAllocated;
            RequireRequiredExpenses = requireRequiredExpenses;
            PreventNegativeAssetBalances = preventNegativeAssetBalances;
            CompletionRemainderTolerance = completionRemainderTolerance;
            MinimumUjeToComplete = minimumUjeToComplete;
        }
    }

    public sealed class PeriodCalculationSettings
    {
        public double DisposableIncome { get; }
        public double CurrentIncomeEcu => DisposableIncome;
        public double BaseIncomeEcu { get; }
        public double BaseUje { get; }
        public double MaximumUje { get; }

        public PeriodCalculationSettings(
            double currentIncomeEcu,
            double baseIncomeEcu,
            double baseUje,
            double maximumUje)
        {
            DisposableIncome = currentIncomeEcu;
            BaseIncomeEcu = baseIncomeEcu;
            BaseUje = baseUje;
            MaximumUje = maximumUje;
        }
    }

    public sealed class PeriodRuntimeDefinition
    {
        public PeriodMeta Meta { get; }
        public IReadOnlyList<PeriodInfoBlockValue> InfoBlockValues { get; }
        public IReadOnlyList<PeriodExpenseDefinition> ExpenseDefinitions { get; }
        public IReadOnlyList<PeriodAssetDefinition> AssetDefinitions { get; }
        public PeriodValidationSettings ValidationSettings { get; }
        public PeriodCalculationSettings CalculationSettings { get; }
        public PeriodEconomyContext EconomyContext { get; }
        public double InitialCashBalance { get; }
        public double InitialDepositBalance { get; }
        public string SourceSummary { get; }

        public PeriodRuntimeDefinition(
            PeriodMeta meta,
            IReadOnlyList<PeriodInfoBlockValue> infoBlockValues,
            IReadOnlyList<PeriodExpenseDefinition> expenseDefinitions,
            IReadOnlyList<PeriodAssetDefinition> assetDefinitions,
            PeriodValidationSettings validationSettings,
            PeriodCalculationSettings calculationSettings,
            PeriodEconomyContext economyContext,
            double initialCashBalance,
            double initialDepositBalance,
            string sourceSummary)
        {
            Meta = meta ?? new PeriodMeta(string.Empty, 0, string.Empty, string.Empty, string.Empty);
            InfoBlockValues = infoBlockValues != null
                ? new List<PeriodInfoBlockValue>(infoBlockValues)
                : Array.Empty<PeriodInfoBlockValue>();
            ExpenseDefinitions = expenseDefinitions != null
                ? new List<PeriodExpenseDefinition>(expenseDefinitions)
                : Array.Empty<PeriodExpenseDefinition>();
            AssetDefinitions = assetDefinitions != null
                ? new List<PeriodAssetDefinition>(assetDefinitions)
                : Array.Empty<PeriodAssetDefinition>();
            ValidationSettings = validationSettings ?? new PeriodValidationSettings(true, true, true, 0.01d, 0d);
            CalculationSettings = calculationSettings ?? new PeriodCalculationSettings(0d, 100d, 0d, 100d);
            EconomyContext = economyContext ?? PeriodEconomyContext.Empty;
            InitialCashBalance = initialCashBalance;
            InitialDepositBalance = initialDepositBalance;
            SourceSummary = sourceSummary ?? string.Empty;
        }
    }

    public sealed class PeriodExpenseState
    {
        public string ExpenseId { get; }
        public double Amount { get; }
        public FundsSourceType Source { get; }

        public PeriodExpenseState(
            string expenseId,
            double amount,
            FundsSourceType source)
        {
            ExpenseId = expenseId ?? string.Empty;
            Amount = amount;
            Source = source;
        }

        public PeriodExpenseState With(
            double? amount = null,
            FundsSourceType? source = null)
        {
            return new PeriodExpenseState(
                ExpenseId,
                amount ?? Amount,
                source ?? Source);
        }
    }

    public sealed class PeriodAssetOperationEntry
    {
        public string OperationId { get; }
        public string AssetId { get; }
        public AssetOperationKind Kind { get; }
        public FundsSourceType Source { get; }
        public double Amount { get; }
        public string CreatedAtUtc { get; }

        public PeriodAssetOperationEntry(
            string operationId,
            string assetId,
            AssetOperationKind kind,
            FundsSourceType source,
            double amount,
            string createdAtUtc)
        {
            OperationId = operationId ?? Guid.NewGuid().ToString("N");
            AssetId = assetId ?? string.Empty;
            Kind = kind;
            Source = source;
            Amount = amount;
            CreatedAtUtc = createdAtUtc ?? string.Empty;
        }
    }

    public sealed class PeriodAssetBalance
    {
        public string AssetId { get; }
        public string Title { get; }
        public PeriodAssetType AssetType { get; }
        public double InitialAmount { get; }
        public double CurrentAmount { get; }
        public bool AllowsDeposit { get; }
        public bool AllowsWithdraw { get; }

        public PeriodAssetBalance(
            string assetId,
            string title,
            PeriodAssetType assetType,
            double initialAmount,
            double currentAmount,
            bool allowsDeposit,
            bool allowsWithdraw)
        {
            AssetId = assetId ?? string.Empty;
            Title = title ?? string.Empty;
            AssetType = assetType;
            InitialAmount = initialAmount;
            CurrentAmount = currentAmount;
            AllowsDeposit = allowsDeposit;
            AllowsWithdraw = allowsWithdraw;
        }
    }

    public sealed class PeriodValidationIssue
    {
        public string Code { get; }
        public string Message { get; }
        public string TargetId { get; }
        public bool IsBlocking { get; }

        public PeriodValidationIssue(
            string code,
            string message,
            string targetId,
            bool isBlocking)
        {
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            TargetId = targetId ?? string.Empty;
            IsBlocking = isBlocking;
        }
    }

    public sealed class UjeBreakdownItem
    {
        public string Id { get; }
        public string Label { get; }
        public double Value { get; }
        public double MaximumValue { get; }
        public double ReferenceAmount { get; }

        public UjeBreakdownItem(
            string id,
            string label,
            double value,
            double maximumValue,
            double referenceAmount)
        {
            Id = id ?? string.Empty;
            Label = label ?? string.Empty;
            Value = value;
            MaximumValue = maximumValue;
            ReferenceAmount = referenceAmount;
        }
    }

    public sealed class PeriodCalculationSummary
    {
        public static PeriodCalculationSummary Empty { get; } = new PeriodCalculationSummary(
            0d,
            0d,
            0d,
            0d,
            0d,
            0d,
            0d,
            0d,
            Array.Empty<UjeBreakdownItem>(),
            Array.Empty<PeriodValidationIssue>(),
            Array.Empty<PeriodAssetBalance>(),
            false);

        public double DisposableIncome { get; }
        public double CurrentIncomeEcu => DisposableIncome;
        public double IncomeAllocatedToExpenses { get; }
        public double IncomeAllocatedToAssets { get; }
        public double TotalExpenses { get; }
        public double RemainingToAllocate { get; }
        public double CashBalance { get; }
        public double EndingCashEcu => CashBalance;
        public double DepositBalance { get; }
        public double EndingDepositEcu => DepositBalance;
        public double Uje { get; }
        public IReadOnlyList<UjeBreakdownItem> UjeBreakdown { get; }
        public IReadOnlyList<PeriodValidationIssue> ValidationIssues { get; }
        public IReadOnlyList<PeriodAssetBalance> AssetBalances { get; }
        public bool CanComplete { get; }

        public PeriodCalculationSummary(
            double disposableIncome,
            double incomeAllocatedToExpenses,
            double incomeAllocatedToAssets,
            double totalExpenses,
            double remainingToAllocate,
            double cashBalance,
            double depositBalance,
            double uje,
            IReadOnlyList<UjeBreakdownItem> ujeBreakdown,
            IReadOnlyList<PeriodValidationIssue> validationIssues,
            IReadOnlyList<PeriodAssetBalance> assetBalances,
            bool canComplete)
        {
            DisposableIncome = disposableIncome;
            IncomeAllocatedToExpenses = incomeAllocatedToExpenses;
            IncomeAllocatedToAssets = incomeAllocatedToAssets;
            TotalExpenses = totalExpenses;
            RemainingToAllocate = remainingToAllocate;
            CashBalance = cashBalance;
            DepositBalance = depositBalance;
            Uje = uje;
            UjeBreakdown = ujeBreakdown != null
                ? new List<UjeBreakdownItem>(ujeBreakdown)
                : Array.Empty<UjeBreakdownItem>();
            ValidationIssues = validationIssues != null
                ? new List<PeriodValidationIssue>(validationIssues)
                : Array.Empty<PeriodValidationIssue>();
            AssetBalances = assetBalances != null
                ? new List<PeriodAssetBalance>(assetBalances)
                : Array.Empty<PeriodAssetBalance>();
            CanComplete = canComplete;
        }
    }

    public sealed class AssetOperationDialogState
    {
        public static AssetOperationDialogState Closed { get; } = new AssetOperationDialogState(
            false,
            string.Empty,
            string.Empty,
            AssetOperationKind.None,
            Array.Empty<FundsSourceType>(),
            FundsSourceType.Unknown,
            0d,
            0d,
            string.Empty,
            string.Empty);

        public bool IsOpen { get; }
        public string AssetId { get; }
        public string AssetTitle { get; }
        public AssetOperationKind Kind { get; }
        public IReadOnlyList<FundsSourceType> AllowedSources { get; }
        public FundsSourceType SelectedSource { get; }
        public double SuggestedAmount { get; }
        public double MaxAmount { get; }
        public string Title { get; }
        public string Subtitle { get; }

        public AssetOperationDialogState(
            bool isOpen,
            string assetId,
            string assetTitle,
            AssetOperationKind kind,
            IReadOnlyList<FundsSourceType> allowedSources,
            FundsSourceType selectedSource,
            double suggestedAmount,
            double maxAmount,
            string title,
            string subtitle)
        {
            IsOpen = isOpen;
            AssetId = assetId ?? string.Empty;
            AssetTitle = assetTitle ?? string.Empty;
            Kind = kind;
            AllowedSources = allowedSources != null
                ? new List<FundsSourceType>(allowedSources)
                : Array.Empty<FundsSourceType>();
            SelectedSource = selectedSource;
            SuggestedAmount = suggestedAmount;
            MaxAmount = maxAmount;
            Title = title ?? string.Empty;
            Subtitle = subtitle ?? string.Empty;
        }

        public AssetOperationDialogState With(
            FundsSourceType? selectedSource = null,
            double? suggestedAmount = null,
            double? maxAmount = null,
            string title = null,
            string subtitle = null)
        {
            return new AssetOperationDialogState(
                IsOpen,
                AssetId,
                AssetTitle,
                Kind,
                AllowedSources,
                selectedSource ?? SelectedSource,
                suggestedAmount ?? SuggestedAmount,
                maxAmount ?? MaxAmount,
                title ?? Title,
                subtitle ?? Subtitle);
        }
    }

    public sealed class PeriodRuntimeState
    {
        public static PeriodRuntimeState Empty { get; } = new PeriodRuntimeState(
            string.Empty,
            0,
            PeriodFlowState.None,
            null,
            Array.Empty<PeriodExpenseState>(),
            Array.Empty<PeriodAssetOperationEntry>(),
            PeriodCalculationSummary.Empty,
            AssetOperationDialogState.Closed,
            false,
            string.Empty,
            string.Empty,
            string.Empty,
            false);

        public string RunId { get; }
        public int PeriodNumber { get; }
        public PeriodFlowState FlowState { get; }
        public PeriodRuntimeDefinition Definition { get; }
        public IReadOnlyList<PeriodExpenseState> Expenses { get; }
        public IReadOnlyList<PeriodAssetOperationEntry> AssetOperations { get; }
        public PeriodCalculationSummary Summary { get; }
        public AssetOperationDialogState AssetDialog { get; }
        public bool IsCheckpointSubmitted { get; }
        public string StatusMessage { get; }
        public string LastError { get; }
        public string SubmittedAtUtc { get; }
        public bool HasPendingLocalChanges { get; }
        public bool HasDefinition => Definition != null;

        public PeriodRuntimeState(
            string runId,
            int periodNumber,
            PeriodFlowState flowState,
            PeriodRuntimeDefinition definition,
            IReadOnlyList<PeriodExpenseState> expenses,
            IReadOnlyList<PeriodAssetOperationEntry> assetOperations,
            PeriodCalculationSummary summary,
            AssetOperationDialogState assetDialog,
            bool isCheckpointSubmitted,
            string statusMessage,
            string lastError,
            string submittedAtUtc,
            bool hasPendingLocalChanges)
        {
            RunId = runId ?? string.Empty;
            PeriodNumber = periodNumber;
            FlowState = flowState;
            Definition = definition;
            Expenses = expenses != null
                ? new List<PeriodExpenseState>(expenses)
                : Array.Empty<PeriodExpenseState>();
            AssetOperations = assetOperations != null
                ? new List<PeriodAssetOperationEntry>(assetOperations)
                : Array.Empty<PeriodAssetOperationEntry>();
            Summary = summary ?? PeriodCalculationSummary.Empty;
            AssetDialog = assetDialog ?? AssetOperationDialogState.Closed;
            IsCheckpointSubmitted = isCheckpointSubmitted;
            StatusMessage = statusMessage ?? string.Empty;
            LastError = lastError ?? string.Empty;
            SubmittedAtUtc = submittedAtUtc ?? string.Empty;
            HasPendingLocalChanges = hasPendingLocalChanges;
        }

        public PeriodRuntimeState With(
            PeriodFlowState? flowState = null,
            IReadOnlyList<PeriodExpenseState> expenses = null,
            IReadOnlyList<PeriodAssetOperationEntry> assetOperations = null,
            PeriodCalculationSummary summary = null,
            AssetOperationDialogState assetDialog = null,
            bool? isCheckpointSubmitted = null,
            string statusMessage = null,
            string lastError = null,
            string submittedAtUtc = null,
            bool? hasPendingLocalChanges = null)
        {
            return new PeriodRuntimeState(
                RunId,
                PeriodNumber,
                flowState ?? FlowState,
                Definition,
                expenses ?? Expenses,
                assetOperations ?? AssetOperations,
                summary ?? Summary,
                assetDialog ?? AssetDialog,
                isCheckpointSubmitted ?? IsCheckpointSubmitted,
                statusMessage ?? StatusMessage,
                lastError ?? LastError,
                submittedAtUtc ?? SubmittedAtUtc,
                hasPendingLocalChanges ?? HasPendingLocalChanges);
        }
    }
}
