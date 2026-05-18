using System;
using System.Collections.Generic;
using Game.Core.Application.Logging;
using Game.Core.Application.Networking;
using Game.Core.Application.Session;
using Game.Domain.GameFlow;

namespace Game.Core.Application.Periods
{
    public sealed class PeriodRuntimeFactory : IPeriodRuntimeFactory
    {
        private readonly IPeriodCalculationEngine _calculationEngine;
        private readonly ISessionPersistenceService _persistenceService;
        private readonly IJsonSerializer _serializer;
        private readonly IAppLogger _logger;

        public PeriodRuntimeFactory(
            IPeriodCalculationEngine calculationEngine,
            ISessionPersistenceService persistenceService,
            IJsonSerializer serializer,
            IAppLogger logger)
        {
            _calculationEngine = calculationEngine;
            _persistenceService = persistenceService;
            _serializer = serializer;
            _logger = logger;
        }

        public bool TryCreateNew(
            ClientRuntimeState clientRuntime,
            out PeriodRuntimeState runtimeState,
            out string error)
        {
            runtimeState = PeriodRuntimeState.Empty;
            error = string.Empty;

            if (!TryBuildDefinition(clientRuntime, out var definition, out var runId, out var periodNumber, out error))
            {
                return false;
            }

            var expenses = BuildInitialExpenseStates(definition);
            var summary = _calculationEngine.Recalculate(definition, expenses, Array.Empty<PeriodAssetOperationEntry>());

            runtimeState = new PeriodRuntimeState(
                runId,
                periodNumber,
                PeriodFlowState.PeriodIntro,
                definition,
                expenses,
                Array.Empty<PeriodAssetOperationEntry>(),
                summary,
                AssetOperationDialogState.Closed,
                false,
                "Период готов к запуску.",
                string.Empty,
                string.Empty,
                true);

            return true;
        }

        public bool TryRestore(
            ClientRuntimeState clientRuntime,
            PersistedPeriodSnapshot snapshot,
            out PeriodRuntimeState runtimeState,
            out string error)
        {
            runtimeState = PeriodRuntimeState.Empty;
            error = string.Empty;

            if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.rawPeriodStateJson))
            {
                error = "Локальный снимок периода пуст.";
                return false;
            }

            if (!TryBuildDefinition(clientRuntime, out var definition, out var runId, out var periodNumber, out error))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(snapshot.runId)
                && !string.Equals(snapshot.runId, runId, StringComparison.Ordinal))
            {
                error = "Локальный снимок периода относится к другому запуску.";
                return false;
            }

            if (snapshot.periodNumber > 0 && snapshot.periodNumber != periodNumber)
            {
                error = "Локальный снимок периода относится к другому периоду.";
                return false;
            }

            if (!_serializer.TryDeserialize(
                    snapshot.rawPeriodStateJson,
                    out PeriodRuntimeSnapshotDto dto,
                    out var deserializeError))
            {
                error = $"Не удалось прочитать локальный снимок периода. {deserializeError}";
                return false;
            }

            if (dto != null && (dto.hasPersistedInitialBalances || dto.hasPersistedAccumulatedUje))
            {
                definition = WithRuntimeOverrides(
                    definition,
                    dto.hasPersistedInitialBalances ? dto.initialCashBalance : (double?)null,
                    dto.hasPersistedInitialBalances ? dto.initialDepositBalance : (double?)null,
                    dto.hasPersistedAccumulatedUje ? dto.accumulatedUje : (double?)null);
            }

            var expenses = RestoreExpenseStates(definition, dto?.expenses);
            var operations = RestoreOperations(dto?.assetOperations);
            var summary = _calculationEngine.Recalculate(definition, expenses, operations);
            var flowState = snapshot.isCheckpointSubmitted
                ? PeriodFlowState.PeriodClosed
                : SanitizeRestoredFlow(dto != null
                    ? PeriodContractMapper.ToPeriodFlowState(dto.flowState)
                    : PeriodContractMapper.ToPeriodFlowState(snapshot.flowState));

            runtimeState = new PeriodRuntimeState(
                runId,
                periodNumber,
                flowState,
                definition,
                expenses,
                operations,
                summary,
                AssetOperationDialogState.Closed,
                snapshot.isCheckpointSubmitted || dto != null && dto.isCheckpointSubmitted,
                snapshot.isCheckpointSubmitted
                    ? "Период уже сохранен."
                    : "Черновик периода восстановлен.",
                dto != null ? dto.lastError : string.Empty,
                dto != null ? dto.submittedAtUtc : string.Empty,
                dto != null && dto.hasPendingLocalChanges);

            return true;
        }

        private bool TryBuildDefinition(
            ClientRuntimeState clientRuntime,
            out PeriodRuntimeDefinition definition,
            out string runId,
            out int periodNumber,
            out string error)
        {
            definition = null;
            runId = string.Empty;
            periodNumber = 0;
            error = string.Empty;

            if (clientRuntime == null || !clientRuntime.HasSession || clientRuntime.Bootstrap == null)
            {
                error = "Сессия еще не загружена.";
                return false;
            }

            runId = clientRuntime.AuthenticatedRun.RunId;
            periodNumber = clientRuntime.Bootstrap.Run != null && clientRuntime.Bootstrap.Run.CurrentPeriodNumber > 0
                ? clientRuntime.Bootstrap.Run.CurrentPeriodNumber
                : 1;

            var sessionConfigRuntime = clientRuntime.Bootstrap.Session.SessionConfig.Runtime ?? SessionConfigRuntime.Empty;
            var configuredPeriod = sessionConfigRuntime.TryGetPeriod(periodNumber, out var resolvedPeriodRuntime)
                ? resolvedPeriodRuntime
                : SessionPeriodRuntime.Empty;
            var sessionDocument = clientRuntime.Bootstrap.Session.SessionConfig.Document;
            var assignedDocument = clientRuntime.Bootstrap.Participant.AssignedConfig.Document;
            var sessionRoot = sessionConfigRuntime.RootNode != null && sessionConfigRuntime.RootNode.Kind != JsonValueKind.Null
                ? sessionConfigRuntime.RootNode
                : sessionDocument != null && sessionDocument.IsValid
                ? sessionDocument.Root
                : JsonValue.Null;
            var assignedRoot = assignedDocument != null && assignedDocument.IsValid
                ? assignedDocument.Root
                : JsonValue.Null;
            var periodNode = configuredPeriod != null
                             && configuredPeriod.RawNode != null
                             && configuredPeriod.RawNode.Kind != JsonValueKind.Null
                ? configuredPeriod.RawNode
                : ResolvePeriodNode(sessionRoot, periodNumber);
            var title = !string.IsNullOrWhiteSpace(configuredPeriod.Title)
                ? configuredPeriod.Title
                : GetString(periodNode, "title", "name", "label");

            if (string.IsNullOrWhiteSpace(title))
            {
                title = $"Период {periodNumber}";
            }

            var periodStatistics = ResolvePeriodStatistics(clientRuntime, periodNumber);
            var hasIncomeLossForCurrentPeriod = HasIncomeLossForPeriod(clientRuntime, sessionConfigRuntime, periodNumber);
            var economyContext = BuildEconomyContext(clientRuntime, periodStatistics, periodNumber, hasIncomeLossForCurrentPeriod);
            var historicalLabel = !string.IsNullOrWhiteSpace(configuredPeriod.HistoricalYear)
                ? configuredPeriod.HistoricalYear
                : !string.IsNullOrWhiteSpace(economyContext.HistoricalYear)
                ? economyContext.HistoricalYear
                : FirstString(
                    periodNode,
                    sessionRoot,
                    "historicalLabel",
                    "historicalPeriodLabel",
                    "timelineLabel",
                    "yearLabel");

            var baseUje = FirstNumber(
                periodNode,
                sessionRoot,
                "baseUje",
                "startingUje",
                "wellbeingBase",
                "energyBase") ?? 0d;
            var maximumUje = FirstNumber(
                periodNode,
                sessionRoot,
                "maxUje",
                "maximumUje",
                "wellbeingMax",
                "energyMax") ?? 0d;
            var minimumUje = FirstNumber(
                periodNode,
                sessionRoot,
                "minimumUje",
                "minUje",
                "minimumEnergy") ?? 0d;

            var validationSettings = BuildValidationSettings(periodNode, sessionRoot, minimumUje);
            var baseExpenseDefinitions = BuildExpenseDefinitions(periodNode, sessionRoot, economyContext);
            var assetDefinitions = BuildAssetDefinitions();
            var activeCredits = ResolveConsumerCredits(runId, periodNumber);
            var initialCash = ResolveInitialAssetBalance(assignedRoot, periodNode, sessionRoot, "cash") ?? 0d;
            var initialDeposit = ResolveInitialAssetBalance(assignedRoot, periodNode, sessionRoot, "deposit") ?? 0d;
            var expenseDefinitions = AppendConsumerCreditExpenseDefinitions(
                baseExpenseDefinitions,
                activeCredits,
                periodNumber);

            definition = new PeriodRuntimeDefinition(
                new PeriodMeta(
                    $"period_{periodNumber}",
                    periodNumber,
                    title,
                    historicalLabel,
                    configuredPeriod.Phase,
                    configuredPeriod.InternalCode,
                    configuredPeriod.EnabledFeatures,
                    clientRuntime.Bootstrap.Session.SessionConfig.Summary),
                BuildStatisticsInfoBlockValues(configuredPeriod, periodNode, sessionRoot, periodStatistics),
                expenseDefinitions,
                assetDefinitions,
                validationSettings,
                new PeriodCalculationSettings(
                    economyContext.CurrentIncomeEcu,
                    economyContext.BaseIncomeEcu,
                    baseUje,
                    maximumUje),
                economyContext,
                activeCredits,
                initialCash,
                initialDeposit,
                periodNode.Kind != JsonValueKind.Null
                    ? "session_config_period"
                    : "fallback_profile");

            return true;
        }

        private static IReadOnlyList<PeriodExpenseState> BuildInitialExpenseStates(PeriodRuntimeDefinition definition)
        {
            var result = new List<PeriodExpenseState>(definition.ExpenseDefinitions.Count);

            foreach (var expenseDefinition in definition.ExpenseDefinitions)
            {
                if (expenseDefinition == null)
                {
                    continue;
                }

                var defaultSource = FundsSourceType.CurrentIncome;

                if (expenseDefinition.AllowedSources != null
                    && expenseDefinition.AllowedSources.Count > 0)
                {
                    defaultSource = expenseDefinition.AllowedSources[0];
                }

                result.Add(new PeriodExpenseState(
                    expenseDefinition.Id,
                    0d,
                    defaultSource));
            }

            return result;
        }

        private static IReadOnlyList<PeriodExpenseState> RestoreExpenseStates(
            PeriodRuntimeDefinition definition,
            IReadOnlyList<PeriodExpenseStateSnapshotDto> snapshots)
        {
            var result = new List<PeriodExpenseState>(definition.ExpenseDefinitions.Count);

            foreach (var expenseDefinition in definition.ExpenseDefinitions)
            {
                if (expenseDefinition == null)
                {
                    continue;
                }

                PeriodExpenseStateSnapshotDto matched = null;

                if (snapshots != null)
                {
                    for (var index = 0; index < snapshots.Count; index++)
                    {
                        var snapshot = snapshots[index];

                        if (snapshot != null
                            && string.Equals(snapshot.expenseId, expenseDefinition.Id, StringComparison.Ordinal))
                        {
                            matched = snapshot;
                            break;
                        }
                    }
                }

                var source = matched != null
                    ? PeriodContractMapper.ToFundsSource(matched.source)
                    : FundsSourceType.Unknown;

                if (source == FundsSourceType.Unknown)
                {
                    source = expenseDefinition.AllowedSources != null && expenseDefinition.AllowedSources.Count > 0
                        ? expenseDefinition.AllowedSources[0]
                        : FundsSourceType.CurrentIncome;
                }

                result.Add(new PeriodExpenseState(
                    expenseDefinition.Id,
                    matched != null ? Math.Max(0d, matched.amount) : 0d,
                    source));
            }

            return result;
        }

        private static IReadOnlyList<PeriodAssetOperationEntry> RestoreOperations(
            IReadOnlyList<PeriodAssetOperationSnapshotDto> snapshots)
        {
            if (snapshots == null || snapshots.Count == 0)
            {
                return Array.Empty<PeriodAssetOperationEntry>();
            }

            var result = new List<PeriodAssetOperationEntry>(snapshots.Count);

            foreach (var snapshot in snapshots)
            {
                if (snapshot == null
                    || string.IsNullOrWhiteSpace(snapshot.assetId)
                    || snapshot.amount <= 0d)
                {
                    continue;
                }

                result.Add(new PeriodAssetOperationEntry(
                    snapshot.operationId,
                    snapshot.assetId,
                    PeriodContractMapper.ToAssetOperationKind(snapshot.kind),
                    PeriodContractMapper.ToFundsSource(snapshot.source),
                    Math.Max(0d, snapshot.amount),
                    snapshot.createdAtUtc));
            }

            return result;
        }

        private IReadOnlyList<ConsumerCreditContractRuntime> ResolveConsumerCredits(string runId, int periodNumber)
        {
            if (_persistenceService == null
                || _serializer == null
                || string.IsNullOrWhiteSpace(runId)
                || periodNumber <= 0
                || !_persistenceService.TryLoadPeriodSnapshot(out var snapshot)
                || snapshot == null
                || !string.Equals(snapshot.runId, runId, StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(snapshot.rawPeriodStateJson))
            {
                return Array.Empty<ConsumerCreditContractRuntime>();
            }

            if (!_serializer.TryDeserialize(
                    snapshot.rawPeriodStateJson,
                    out PeriodRuntimeSnapshotDto dto,
                    out var deserializeError))
            {
                _logger.Warning($"Failed to deserialize persisted consumer credit snapshot. {deserializeError}");
                return Array.Empty<ConsumerCreditContractRuntime>();
            }

            var currentCredits = RestoreConsumerCredits(dto != null ? dto.consumerCredits : null);

            if (currentCredits.Count == 0)
            {
                return Array.Empty<ConsumerCreditContractRuntime>();
            }

            if (snapshot.periodNumber == periodNumber)
            {
                return currentCredits;
            }

            if (snapshot.isCheckpointSubmitted && snapshot.periodNumber == periodNumber - 1)
            {
                return AdvanceConsumerCreditsForNextPeriod(
                    snapshot.periodNumber,
                    currentCredits,
                    dto != null ? dto.expenses : null);
            }

            return Array.Empty<ConsumerCreditContractRuntime>();
        }

        private static IReadOnlyList<ConsumerCreditContractRuntime> RestoreConsumerCredits(
            IReadOnlyList<ConsumerCreditContractSnapshotDto> snapshots)
        {
            if (snapshots == null || snapshots.Count == 0)
            {
                return Array.Empty<ConsumerCreditContractRuntime>();
            }

            var result = new List<ConsumerCreditContractRuntime>(snapshots.Count);

            for (var index = 0; index < snapshots.Count; index++)
            {
                var snapshot = snapshots[index];

                if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.creditId))
                {
                    continue;
                }

                result.Add(new ConsumerCreditContractRuntime(
                    string.IsNullOrWhiteSpace(snapshot.contractType)
                        ? ConsumerCreditMath.ConsumerCreditKind
                        : snapshot.contractType,
                    snapshot.creditId,
                    snapshot.originationPeriodNumber,
                    Math.Max(0d, snapshot.originalPrincipal),
                    Math.Max(0d, snapshot.remainingPrincipal),
                    Math.Max(0d, snapshot.periodicPayment),
                    snapshot.fixedRatePercent,
                    Math.Max(0, snapshot.remainingPeriods)));
            }

            return result;
        }

        private static IReadOnlyList<ConsumerCreditContractRuntime> AdvanceConsumerCreditsForNextPeriod(
            int closedPeriodNumber,
            IReadOnlyList<ConsumerCreditContractRuntime> credits,
            IReadOnlyList<PeriodExpenseStateSnapshotDto> expenseSnapshots)
        {
            if (credits == null || credits.Count == 0)
            {
                return Array.Empty<ConsumerCreditContractRuntime>();
            }

            var result = new List<ConsumerCreditContractRuntime>(credits.Count);

            for (var index = 0; index < credits.Count; index++)
            {
                var credit = credits[index];

                if (credit == null
                    || string.IsNullOrWhiteSpace(credit.CreditId)
                    || credit.RemainingPeriods <= 0
                    || credit.RemainingPrincipal <= 0.01d)
                {
                    continue;
                }

                if (closedPeriodNumber <= credit.OriginationPeriodNumber)
                {
                    result.Add(credit);
                    continue;
                }

                var paymentAmount = ResolveCreditPaymentAmount(
                    string.IsNullOrWhiteSpace(credit.ContractType)
                        ? ConsumerCreditMath.ConsumerCreditKind
                        : credit.ContractType,
                    credit.CreditId,
                    credit.PeriodicPayment,
                    expenseSnapshots);
                var nextRemainingPrincipal = ConsumerCreditMath.CalculateNextRemainingPrincipal(
                    credit.RemainingPrincipal,
                    paymentAmount,
                    credit.FixedRatePercent);
                var nextRemainingPeriods = Math.Max(0, credit.RemainingPeriods - 1);

                if (nextRemainingPeriods <= 0 || nextRemainingPrincipal <= 0.01d)
                {
                    continue;
                }

                result.Add(new ConsumerCreditContractRuntime(
                    string.IsNullOrWhiteSpace(credit.ContractType)
                        ? ConsumerCreditMath.ConsumerCreditKind
                        : credit.ContractType,
                    credit.CreditId,
                    credit.OriginationPeriodNumber,
                    credit.OriginalPrincipal,
                    nextRemainingPrincipal,
                    credit.PeriodicPayment,
                    credit.FixedRatePercent,
                    nextRemainingPeriods));
            }

            return result;
        }

        private static double ResolveCreditPaymentAmount(
            string contractType,
            string creditId,
            double fallbackPaymentAmount,
            IReadOnlyList<PeriodExpenseStateSnapshotDto> expenseSnapshots)
        {
            if (expenseSnapshots == null || expenseSnapshots.Count == 0)
            {
                return Math.Max(0d, fallbackPaymentAmount);
            }

            var expenseId = ConsumerCreditMath.BuildExpenseId(contractType, creditId);

            for (var index = 0; index < expenseSnapshots.Count; index++)
            {
                var snapshot = expenseSnapshots[index];

                if (snapshot == null
                    || !string.Equals(snapshot.expenseId, expenseId, StringComparison.Ordinal))
                {
                    continue;
                }

                return Math.Max(0d, snapshot.amount);
            }

            return Math.Max(0d, fallbackPaymentAmount);
        }

        private static IReadOnlyList<PeriodExpenseDefinition> AppendConsumerCreditExpenseDefinitions(
            IReadOnlyList<PeriodExpenseDefinition> baseDefinitions,
            IReadOnlyList<ConsumerCreditContractRuntime> activeCredits,
            int periodNumber)
        {
            var result = new List<PeriodExpenseDefinition>();

            if (baseDefinitions != null)
            {
                for (var index = 0; index < baseDefinitions.Count; index++)
                {
                    var definition = baseDefinitions[index];

                    if (definition != null)
                    {
                        result.Add(definition);
                    }
                }
            }

            if (activeCredits == null || activeCredits.Count == 0)
            {
                return result;
            }

            for (var index = 0; index < activeCredits.Count; index++)
            {
                var credit = activeCredits[index];

                if (credit == null
                    || string.IsNullOrWhiteSpace(credit.CreditId)
                    || periodNumber <= credit.OriginationPeriodNumber
                    || credit.RemainingPeriods <= 0
                    || credit.RemainingPrincipal <= 0.01d)
                {
                    continue;
                }

                result.Add(new PeriodExpenseDefinition(
                    ConsumerCreditMath.BuildExpenseId(
                        string.IsNullOrWhiteSpace(credit.ContractType)
                            ? ConsumerCreditMath.ConsumerCreditKind
                            : credit.ContractType,
                        credit.CreditId),
                    string.Equals(credit.ContractType, ConsumerCreditMath.MortgageKind, StringComparison.Ordinal)
                        ? "Платёж по ипотеке"
                        : "Платёж по кредиту",
                    true,
                    credit.PeriodicPayment,
                    credit.PeriodicPayment,
                    credit.PeriodicPayment,
                    new[] { FundsSourceType.CurrentIncome, FundsSourceType.Cash },
                    0d,
                    0d,
                    string.Empty));
            }

            return result;
        }

        private PeriodValidationSettings BuildValidationSettings(
            JsonValue periodNode,
            JsonValue sessionRoot,
            double minimumUje)
        {
            var validationNode = periodNode.GetObjectCandidate("validation", "rules");

            if (validationNode.Kind == JsonValueKind.Null)
            {
                validationNode = sessionRoot.FindFirstDescendantProperty("validation", "rules");
            }

            var requireAllIncomeAllocated = GetBoolean(validationNode, "requireAllIncomeAllocated", "mustAllocateAllIncome")
                ?? true;
            var requireRequiredExpenses = GetBoolean(validationNode, "requireRequiredExpenses", "mandatoryExpensesRequired")
                ?? true;
            var preventNegativeBalances = GetBoolean(validationNode, "preventNegativeAssetBalances", "preventNegativeBalances")
                ?? true;
            var tolerance = GetNumber(validationNode, "completionRemainderTolerance", "tolerance", "incomeTolerance")
                ?? 0.01d;

            return new PeriodValidationSettings(
                requireAllIncomeAllocated,
                requireRequiredExpenses,
                preventNegativeBalances,
                tolerance,
                minimumUje);
        }

        private IReadOnlyList<PeriodInfoBlockValue> BuildInfoBlockValues(
            JsonValue periodNode,
            JsonValue sessionRoot,
            PeriodStatisticsRuntime periodStatistics)
        {
            return new[]
            {
                CreateInfoValue(periodNode, sessionRoot, "inflation", "Инфляция", "%", "inflation", "inflationRate"),
                CreateInfoValue(periodNode, sessionRoot, "income_growth", "Рост дохода", "%", "nominalIncomeGrowth", "incomeGrowth", "salaryGrowth", "incomeGrowthRate"),
                CreateInfoValue(periodNode, sessionRoot, "deposit_rate", "Ставка по депозиту", "%", "depositRate", "depositInterestRate", "savingsRate")
            };
        }

        private PeriodInfoBlockValue CreateInfoValue(
            JsonValue periodNode,
            JsonValue sessionRoot,
            string id,
            string label,
            string suffix,
            params string[] candidateNames)
        {
            var number = FirstNumber(periodNode, sessionRoot, candidateNames);
            var text = string.Empty;

            if (!number.HasValue)
            {
                text = FirstString(periodNode, sessionRoot, candidateNames);
            }

            return new PeriodInfoBlockValue(
                id,
                label,
                number,
                suffix,
                text);
        }

        private IReadOnlyList<PeriodInfoBlockValue> BuildStatisticsInfoBlockValues(
            SessionPeriodRuntime configuredPeriod,
            JsonValue periodNode,
            JsonValue sessionRoot,
            PeriodStatisticsRuntime periodStatistics)
        {
            var result = new List<PeriodInfoBlockValue>();

            if (configuredPeriod != null
                && configuredPeriod.InfoBlock != null
                && configuredPeriod.InfoBlock.Entries != null)
            {
                for (var index = 0; index < configuredPeriod.InfoBlock.Entries.Count; index++)
                {
                    var entry = configuredPeriod.InfoBlock.Entries[index];

                    if (entry == null || string.IsNullOrWhiteSpace(entry.Id))
                    {
                        continue;
                    }

                    result.Add(new PeriodInfoBlockValue(
                        entry.Id,
                        string.IsNullOrWhiteSpace(entry.Label) ? entry.Id : entry.Label,
                        entry.NumericValue,
                        entry.Suffix,
                        entry.RawText));
                }
            }

            AddInfoValueIfMissing(
                result,
                CreateStatisticsInfoValue(
                    periodNode,
                    sessionRoot,
                    "inflation",
                    "Инфляция",
                    periodStatistics != null ? periodStatistics.Inflation : null,
                    "%",
                    "inflation",
                    "inflationRate"));
            AddInfoValueIfMissing(
                result,
                CreateStatisticsInfoValue(
                    periodNode,
                    sessionRoot,
                    "income_growth",
                    "Номинальный рост дохода",
                    periodStatistics != null ? periodStatistics.NominalIncomeGrowth : null,
                    string.Empty,
                    "nominalIncomeGrowth",
                    "incomeGrowth",
                    "salaryGrowth",
                    "incomeGrowthRate"));
            AddInfoValueIfMissing(
                result,
                CreateStatisticsInfoValue(
                    periodNode,
                    sessionRoot,
                    "deposit_rate",
                    "Ставка по депозиту",
                    periodStatistics != null ? periodStatistics.DepositRate : null,
                    "%",
                    "depositRate",
                    "depositInterestRate",
                    "savingsRate"));
            if (configuredPeriod != null && configuredPeriod.HasFeature("consumer_credit"))
            {
                AddInfoValueIfMissing(
                    result,
                    CreateStatisticsInfoValue(
                        periodNode,
                        sessionRoot,
                        "credit_rate",
                        "Ставка по кредиту",
                        periodStatistics != null ? periodStatistics.CreditRate : null,
                        "%",
                        "creditRate",
                        "consumerCreditRate",
                        "loanRate"));
            }
            if (configuredPeriod != null && configuredPeriod.HasFeature("mortgage"))
            {
                AddInfoValueIfMissing(
                    result,
                    CreateStatisticsInfoValue(
                        periodNode,
                        sessionRoot,
                        "mortgage_rate",
                        "Ставка по ипотеке",
                        periodStatistics != null ? periodStatistics.MortgageRate : null,
                        "%",
                        "mortgageRate",
                        "homeLoanRate"));
            }

            return result;
        }

        private IReadOnlyList<PeriodInfoBlockValue> BuildStatisticsInfoBlockValues(
            JsonValue periodNode,
            JsonValue sessionRoot,
            PeriodStatisticsRuntime periodStatistics)
        {
            return new[]
            {
                CreateStatisticsInfoValue(
                    periodNode,
                    sessionRoot,
                    "inflation",
                    "Инфляция",
                    periodStatistics != null ? periodStatistics.Inflation : null,
                    "%",
                    "inflation",
                    "inflationRate"),
                CreateStatisticsInfoValue(
                    periodNode,
                    sessionRoot,
                    "income_growth",
                    "Номинальный рост дохода",
                    periodStatistics != null ? periodStatistics.NominalIncomeGrowth : null,
                    string.Empty,
                    "nominalIncomeGrowth",
                    "incomeGrowth",
                    "salaryGrowth",
                    "incomeGrowthRate"),
                CreateStatisticsInfoValue(
                    periodNode,
                    sessionRoot,
                    "deposit_rate",
                    "Ставка по депозиту",
                    periodStatistics != null ? periodStatistics.DepositRate : null,
                    "%",
                    "depositRate",
                    "depositInterestRate",
                    "savingsRate")
            };
        }

        private PeriodInfoBlockValue CreateStatisticsInfoValue(
            JsonValue periodNode,
            JsonValue sessionRoot,
            string id,
            string label,
            double? preferredNumber,
            string suffix,
            params string[] candidateNames)
        {
            var number = preferredNumber ?? FirstNumber(periodNode, sessionRoot, candidateNames);
            var text = string.Empty;

            if (!number.HasValue)
            {
                text = FirstString(periodNode, sessionRoot, candidateNames);
            }

            return new PeriodInfoBlockValue(
                id,
                label,
                number,
                suffix,
                text);
        }

        private IReadOnlyList<PeriodExpenseDefinition> BuildExpenseDefinitions(
            JsonValue periodNode,
            JsonValue sessionRoot,
            PeriodEconomyContext economyContext)
        {
            var result = new List<PeriodExpenseDefinition>();
            var expenseNodes = periodNode.FindArrayDescendant("expenses", "availableExpenses", "expenseDefinitions", "spendingCategories");

            if (expenseNodes.Count == 0)
            {
                expenseNodes = sessionRoot.FindArrayDescendant("expenses", "availableExpenses", "expenseDefinitions", "spendingCategories");
            }

            foreach (var expenseNode in expenseNodes)
            {
                if (expenseNode == null || expenseNode.Kind != JsonValueKind.Object)
                {
                    continue;
                }

                var rawId = GetString(expenseNode, "id", "code", "key", "slug");
                var title = GetString(expenseNode, "title", "name", "label");
                var expenseId = NormalizeExpenseId(rawId, title);

                if (string.IsNullOrWhiteSpace(expenseId) || HasExpense(result, expenseId))
                {
                    continue;
                }

                var defaults = GetFallbackExpense(expenseId);
                var required = GetBoolean(expenseNode, "required", "mandatory", "isRequired") ?? defaults.IsRequired;
                var rawDefaultAmount = GetNumber(expenseNode, "defaultAmount", "initialAmount", "amount") ?? defaults.DefaultAmount;
                var rawMinimumAmount = GetNumber(expenseNode, "minimumAmount", "minAmount", "requiredAmount") ?? defaults.MinimumAmount;
                var rawMaximumAmount = GetNumber(expenseNode, "maximumAmount", "maxAmount", "limitAmount") ?? defaults.MaximumAmount;
                var ujeWeight = GetNumber(expenseNode, "ujeWeight", "wellbeingWeight", "scoreWeight") ?? defaults.UjeWeight;
                var rawUjeReference = GetNumber(expenseNode, "ujeReferenceAmount", "targetAmount", "referenceAmount", "saturationAmount")
                    ?? defaults.UjeReferenceAmount;
                var allowedSources = ParseAllowedSources(expenseNode.FindFirstDescendantProperty("allowedSources", "sources", "fundSources"));
                rawMinimumAmount = ApplyRequiredMinimumOverride(expenseId, required, rawMinimumAmount);
                ApplyFixedExpenseBounds(expenseId, rawMinimumAmount, ref rawMaximumAmount);
                var inflationMultiplier = economyContext != null
                    ? Math.Max(0.0001d, economyContext.ExpenseInflationMultiplier)
                    : 1d;
                var defaultAmount = ApplyInflationMultiplier(rawDefaultAmount, inflationMultiplier);
                var minimumAmount = ApplyInflationMultiplier(rawMinimumAmount, inflationMultiplier);
                var maximumAmount = rawMaximumAmount > 0d
                    ? ApplyInflationMultiplier(rawMaximumAmount, inflationMultiplier)
                    : 0d;
                var ujeReference = ApplyInflationMultiplier(rawUjeReference, inflationMultiplier);

                if (allowedSources.Count == 0)
                {
                    allowedSources = defaults.AllowedSources;
                }

                result.Add(new PeriodExpenseDefinition(
                    expenseId,
                    string.IsNullOrWhiteSpace(title) ? defaults.Title : title,
                    required,
                    defaultAmount,
                    minimumAmount,
                    maximumAmount,
                    allowedSources,
                    ujeWeight,
                    ujeReference,
                    GetString(expenseNode, "description", "hint")));
            }

            EnsureExpense(result, "goods_services", economyContext);
            EnsureExpense(result, "housing_rent", economyContext);
            EnsureExpense(result, "leisure", economyContext);
            EnsureExpense(result, "holiday", economyContext);

            return SortExpenses(result);
        }

        private static IReadOnlyList<PeriodAssetDefinition> BuildAssetDefinitions()
        {
            return new[]
            {
                new PeriodAssetDefinition(
                    "cash",
                    "Наличные",
                    PeriodAssetType.Cash,
                    true,
                    false,
                    new[] { FundsSourceType.CurrentIncome },
                    "Резерв свободных средств внутри периода."),
                new PeriodAssetDefinition(
                    "deposit",
                    "Депозит",
                    PeriodAssetType.Deposit,
                    true,
                    true,
                    new[] { FundsSourceType.CurrentIncome, FundsSourceType.Cash },
                    "Сбережение периода.")
            };
        }

        private static PeriodStatisticsRuntime ResolvePeriodStatistics(ClientRuntimeState clientRuntime, int periodNumber)
        {
            if (clientRuntime == null
                || !clientRuntime.HasSession
                || clientRuntime.Bootstrap == null
                || clientRuntime.Bootstrap.StatisticalDataset == null)
            {
                return PeriodStatisticsRuntime.Empty;
            }

            return clientRuntime.Bootstrap.StatisticalDataset.TryGetPeriodStatistics(periodNumber, out var periodStatistics)
                ? periodStatistics
                : PeriodStatisticsRuntime.Empty;
        }

        private static PeriodEconomyContext BuildEconomyContext(
            ClientRuntimeState clientRuntime,
            PeriodStatisticsRuntime periodStatistics,
            int periodNumber,
            bool hasPermanentIncomeLoss)
        {
            var baseIncomeEcu = clientRuntime != null
                                && clientRuntime.HasSession
                                && clientRuntime.Bootstrap != null
                                && clientRuntime.Bootstrap.Session != null
                                && clientRuntime.Bootstrap.Session.SessionConfig != null
                                && clientRuntime.Bootstrap.Session.SessionConfig.Runtime != null
                                && clientRuntime.Bootstrap.Session.SessionConfig.Runtime.GlobalSettings != null
                                && clientRuntime.Bootstrap.Session.SessionConfig.Runtime.GlobalSettings.BaseIncomeEcu.HasValue
                ? Math.Max(0.0001d, clientRuntime.Bootstrap.Session.SessionConfig.Runtime.GlobalSettings.BaseIncomeEcu.Value)
                : 100d;
            var nominalIncomeGrowth = periodStatistics != null && periodStatistics.NominalIncomeGrowth.HasValue && periodStatistics.NominalIncomeGrowth.Value > 0d
                ? periodStatistics.NominalIncomeGrowth
                : null;
            var inflation = periodStatistics != null && periodStatistics.Inflation.HasValue && periodStatistics.Inflation.Value > 0d
                ? periodStatistics.Inflation
                : null;
            var depositRate = periodStatistics != null && periodStatistics.DepositRate.HasValue && periodStatistics.DepositRate.Value > 0d
                ? periodStatistics.DepositRate
                : null;
            var currentIncomeEcu = baseIncomeEcu;
            var expenseInflationMultiplier = 1d;
            var currentInflationRate = NormalizePercentageToRate(inflation);

            if (periodNumber > 1)
            {
                for (var index = 2; index <= periodNumber; index++)
                {
                    var indexedStatistics = ResolvePeriodStatistics(clientRuntime, index);
                    var growthMultiplier = indexedStatistics != null
                                           && indexedStatistics.NominalIncomeGrowth.HasValue
                                           && indexedStatistics.NominalIncomeGrowth.Value > 0d
                        ? indexedStatistics.NominalIncomeGrowth.Value
                        : 1d;
                    var inflationMultiplier = indexedStatistics != null
                                              && indexedStatistics.Inflation.HasValue
                                              && indexedStatistics.Inflation.Value > 0d
                        ? 1d + NormalizePercentageToRate(indexedStatistics.Inflation)
                        : 1d;

                    currentIncomeEcu *= Math.Max(0.0001d, growthMultiplier);
                    expenseInflationMultiplier *= Math.Max(0.0001d, inflationMultiplier);
                }
            }

            if (hasPermanentIncomeLoss)
            {
                currentIncomeEcu = 0d;
            }

            var currentInflationMultiplier = 1d + currentInflationRate;
            var cashValueMultiplier = 1d;
            var depositValueMultiplier = 1d;

            return new PeriodEconomyContext(
                periodNumber,
                periodStatistics != null ? periodStatistics.HistoricalYear : string.Empty,
                nominalIncomeGrowth,
                inflation,
                depositRate,
                periodStatistics != null ? periodStatistics.CreditRate : null,
                periodStatistics != null ? periodStatistics.MortgageRate : null,
                baseIncomeEcu,
                currentIncomeEcu,
                expenseInflationMultiplier,
                currentInflationMultiplier,
                cashValueMultiplier,
                depositValueMultiplier,
                hasPermanentIncomeLoss);
        }

        private bool HasIncomeLossForPeriod(
            ClientRuntimeState clientRuntime,
            SessionConfigRuntime sessionConfigRuntime,
            int periodNumber)
        {
            if (_persistenceService == null
                || clientRuntime == null
                || !clientRuntime.HasSession
                || periodNumber <= 0
                || !_persistenceService.TryLoadSessionFlowProgress(out var snapshot)
                || snapshot == null)
            {
                return false;
            }

            if (!string.Equals(snapshot.runId, clientRuntime.AuthenticatedRun.RunId, StringComparison.Ordinal))
            {
                return false;
            }

            if (clientRuntime.Bootstrap == null
                || clientRuntime.Bootstrap.Session == null
                || snapshot.sessionConfigVersion != clientRuntime.Bootstrap.Session.ConfigVersion)
            {
                return false;
            }

            return snapshot.schemaVersion == (sessionConfigRuntime != null ? sessionConfigRuntime.SchemaVersion : 0)
                && (snapshot.incomeLossPeriodNumber > 0
                    ? snapshot.incomeLossPeriodNumber == periodNumber
                    : snapshot.hasPermanentIncomeLoss && snapshot.activePeriodNumber == periodNumber);
        }

        private static double NormalizePercentageToRate(double? rawPercent)
        {
            if (!rawPercent.HasValue || rawPercent.Value <= 0d)
            {
                return 0d;
            }

            return rawPercent.Value / 100d;
        }

        private static PeriodFlowState SanitizeRestoredFlow(PeriodFlowState value)
        {
            switch (value)
            {
                case PeriodFlowState.PeriodClosed:
                    return PeriodFlowState.PeriodClosed;
                case PeriodFlowState.PeriodCheckpointSubmitting:
                case PeriodFlowState.PeriodClosing:
                case PeriodFlowState.PeriodValidation:
                    return PeriodFlowState.PeriodActive;
                case PeriodFlowState.PeriodIntro:
                case PeriodFlowState.PeriodActive:
                    return value;
                default:
                    return PeriodFlowState.PeriodActive;
            }
        }

        private static JsonValue ResolvePeriodNode(JsonValue sessionRoot, int periodNumber)
        {
            var periodArray = sessionRoot.FindFirstDescendantProperty("periods", "periodDefinitions", "periodConfigs");

            if (periodArray.Kind == JsonValueKind.Array)
            {
                JsonValue firstObject = JsonValue.Null;
                JsonValue indexedObject = JsonValue.Null;
                var currentIndex = 0;

                foreach (var item in periodArray.ArrayValue)
                {
                    if (item == null || item.Kind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    if (firstObject.Kind == JsonValueKind.Null)
                    {
                        firstObject = item;
                    }

                    currentIndex++;

                    if (indexedObject.Kind == JsonValueKind.Null && currentIndex == periodNumber)
                    {
                        indexedObject = item;
                    }

                    var itemNumber = GetNumber(item, "periodNumber", "number", "index", "id");

                    if (itemNumber.HasValue && Math.Abs(itemNumber.Value - periodNumber) < 0.001d)
                    {
                        return item;
                    }
                }

                if (indexedObject.Kind != JsonValueKind.Null)
                {
                    return indexedObject;
                }

                if (firstObject.Kind != JsonValueKind.Null)
                {
                    return firstObject;
                }
            }

            return sessionRoot.Kind == JsonValueKind.Object
                ? sessionRoot
                : JsonValue.Null;
        }

        private static IReadOnlyList<FundsSourceType> ParseAllowedSources(JsonValue node)
        {
            var result = new List<FundsSourceType>();

            if (node.Kind == JsonValueKind.Array)
            {
                foreach (var item in node.ArrayValue)
                {
                    var source = item.Kind == JsonValueKind.Object
                        ? PeriodContractMapper.ToFundsSource(GetString(item, "id", "code", "key", "source"))
                        : PeriodContractMapper.ToFundsSource(item.GetStringOrDefault());

                    if (source == FundsSourceType.Unknown || result.Contains(source))
                    {
                        continue;
                    }

                    result.Add(source);
                }
            }
            else if (node.Kind == JsonValueKind.String)
            {
                var source = PeriodContractMapper.ToFundsSource(node.StringValue);

                if (source != FundsSourceType.Unknown)
                {
                    result.Add(source);
                }
            }

            return result;
        }

        private static bool HasExpense(IReadOnlyList<PeriodExpenseDefinition> definitions, string expenseId)
        {
            if (definitions == null)
            {
                return false;
            }

            foreach (var definition in definitions)
            {
                if (definition != null && string.Equals(definition.Id, expenseId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static void EnsureExpense(
            ICollection<PeriodExpenseDefinition> definitions,
            string expenseId,
            PeriodEconomyContext economyContext)
        {
            foreach (var definition in definitions)
            {
                if (definition != null && string.Equals(definition.Id, expenseId, StringComparison.Ordinal))
                {
                    return;
                }
            }

            definitions.Add(ApplyEconomyContextToExpenseDefinition(GetFallbackExpense(expenseId), economyContext));
        }

        private static void AddInfoValueIfMissing(
            ICollection<PeriodInfoBlockValue> values,
            PeriodInfoBlockValue candidate)
        {
            if (values == null || candidate == null || string.IsNullOrWhiteSpace(candidate.Id))
            {
                return;
            }

            foreach (var value in values)
            {
                if (value != null
                    && string.Equals(value.Id, candidate.Id, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            values.Add(candidate);
        }

        private static PeriodExpenseDefinition ApplyEconomyContextToExpenseDefinition(
            PeriodExpenseDefinition definition,
            PeriodEconomyContext economyContext)
        {
            if (definition == null)
            {
                return null;
            }

            var inflationMultiplier = economyContext != null
                ? Math.Max(0.0001d, economyContext.ExpenseInflationMultiplier)
                : 1d;

            return new PeriodExpenseDefinition(
                definition.Id,
                definition.Title,
                definition.IsRequired,
                ApplyInflationMultiplier(definition.DefaultAmount, inflationMultiplier),
                ApplyInflationMultiplier(definition.MinimumAmount, inflationMultiplier),
                definition.MaximumAmount > 0d
                    ? ApplyInflationMultiplier(definition.MaximumAmount, inflationMultiplier)
                    : 0d,
                definition.AllowedSources,
                definition.UjeWeight,
                ApplyInflationMultiplier(definition.UjeReferenceAmount, inflationMultiplier),
                definition.Description);
        }

        private static IReadOnlyList<PeriodExpenseDefinition> SortExpenses(IReadOnlyList<PeriodExpenseDefinition> definitions)
        {
            var ordered = new List<PeriodExpenseDefinition>();
            var preferredOrder = new[]
            {
                "goods_services",
                "housing_rent",
                "leisure",
                "holiday"
            };

            foreach (var preferredId in preferredOrder)
            {
                foreach (var definition in definitions)
                {
                    if (definition != null
                        && string.Equals(definition.Id, preferredId, StringComparison.Ordinal))
                    {
                        ordered.Add(definition);
                    }
                }
            }

            foreach (var definition in definitions)
            {
                if (definition == null || ordered.Contains(definition))
                {
                    continue;
                }

                ordered.Add(definition);
            }

            return ordered;
        }

        private static double ApplyInflationMultiplier(double amount, double multiplier)
        {
            if (amount <= 0d)
            {
                return 0d;
            }

            return amount * Math.Max(0.0001d, multiplier);
        }

        private static double ApplyRequiredMinimumOverride(string expenseId, bool isRequired, double minimumAmount)
        {
            if (!isRequired)
            {
                return minimumAmount;
            }

            switch (expenseId)
            {
                case "goods_services":
                case "housing_rent":
                    return Math.Max(20d, minimumAmount);
                default:
                    return minimumAmount;
            }
        }

        private static void ApplyFixedExpenseBounds(string expenseId, double minimumAmount, ref double maximumAmount)
        {
            switch (expenseId)
            {
                case "housing_rent":
                    maximumAmount = minimumAmount > 0d
                        ? minimumAmount
                        : maximumAmount;
                    break;
                case "holiday":
                    maximumAmount = maximumAmount > 0d
                        ? maximumAmount
                        : 10d;
                    break;
            }
        }

        private static PeriodExpenseDefinition GetFallbackExpense(string expenseId)
        {
            switch (expenseId)
            {
                case "housing_rent":
                    return new PeriodExpenseDefinition(
                        "housing_rent",
                        "Аренда жилья",
                        true,
                        0d,
                        20d,
                        20d,
                        new[] { FundsSourceType.CurrentIncome, FundsSourceType.Cash },
                        0d,
                        20d,
                        "Обязательная статья периода.");
                case "leisure":
                    return new PeriodExpenseDefinition(
                        "leisure",
                        "Развлечения и отдых",
                        false,
                        0d,
                        0d,
                        0d,
                        new[] { FundsSourceType.CurrentIncome, FundsSourceType.Cash },
                        0d,
                        100d,
                        string.Empty);
                case "holiday":
                    return new PeriodExpenseDefinition(
                        "holiday",
                        "Деньги на праздник",
                        false,
                        0d,
                        0d,
                        10d,
                        new[] { FundsSourceType.CurrentIncome, FundsSourceType.Cash },
                        0d,
                        10d,
                        string.Empty);
                case "goods_services":
                default:
                    return new PeriodExpenseDefinition(
                        "goods_services",
                        "Покупка товаров и услуг",
                        true,
                        0d,
                        20d,
                        0d,
                        new[] { FundsSourceType.CurrentIncome, FundsSourceType.Cash },
                        0d,
                        60d,
                        "Базовая бытовая статья расходов.");
            }
        }

        private static string NormalizeExpenseId(string rawId, string title)
        {
            var source = $"{rawId} {title}".Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(source))
            {
                return string.Empty;
            }

            if (ContainsAny(source, "rent", "housing", "аренд", "жиль"))
            {
                return "housing_rent";
            }

            if (ContainsAny(source, "entertain", "leisure", "rest", "отдых", "развлеч"))
            {
                return "leisure";
            }

            if (ContainsAny(source, "holiday", "celebr", "fest", "празд"))
            {
                return "holiday";
            }

            if (ContainsAny(source, "goods", "service", "товар", "услуг", "shopping", "purchase"))
            {
                return "goods_services";
            }

            return (rawId ?? title ?? string.Empty)
                .Trim()
                .ToLowerInvariant()
                .Replace(" ", "_")
                .Replace("-", "_");
        }

        private static bool ContainsAny(string source, params string[] parts)
        {
            foreach (var part in parts)
            {
                if (!string.IsNullOrWhiteSpace(part)
                    && source.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static double? ResolveInitialAssetBalance(
            JsonValue assignedRoot,
            JsonValue periodNode,
            JsonValue sessionRoot,
            string assetId)
        {
            var directValue = FirstNumber(
                assignedRoot,
                periodNode,
                sessionRoot,
                $"{assetId}Balance",
                $"{assetId}Amount",
                $"initial{Capitalize(assetId)}",
                $"{assetId}Value");

            if (directValue.HasValue)
            {
                return directValue.Value;
            }

            return ResolveAssetObjectValue(assignedRoot, assetId)
                   ?? ResolveAssetObjectValue(periodNode, assetId)
                   ?? ResolveAssetObjectValue(sessionRoot, assetId);
        }

        private static double? ResolveAssetObjectValue(JsonValue root, string assetId)
        {
            var assetsNode = root.FindFirstDescendantProperty("assets", "balances", "assetBalances", "startingAssets");

            if (assetsNode.Kind == JsonValueKind.Object)
            {
                if (assetsNode.TryGetPropertyIgnoreCase(assetId, out var assetNode))
                {
                    var value = assetNode.AsNullableNumber();

                    if (value.HasValue)
                    {
                        return value;
                    }

                    return GetNumber(assetNode, "value", "amount", "balance", "initialAmount");
                }
            }

            if (assetsNode.Kind == JsonValueKind.Array)
            {
                foreach (var item in assetsNode.ArrayValue)
                {
                    if (item == null || item.Kind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    var itemKey = GetString(item, "id", "code", "key", "assetId", "name");

                    if (!string.Equals(itemKey, assetId, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    return GetNumber(item, "value", "amount", "balance", "initialAmount");
                }
            }

            return null;
        }

        private static string Capitalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return char.ToUpperInvariant(value[0]) + value.Substring(1);
        }

        private static PeriodRuntimeDefinition WithRuntimeOverrides(
            PeriodRuntimeDefinition definition,
            double? initialCashBalance = null,
            double? initialDepositBalance = null,
            double? accumulatedUje = null)
        {
            if (definition == null)
            {
                return null;
            }

            var settings = definition.CalculationSettings;
            return new PeriodRuntimeDefinition(
                definition.Meta,
                definition.InfoBlockValues,
                definition.ExpenseDefinitions,
                definition.AssetDefinitions,
                definition.ValidationSettings,
                new PeriodCalculationSettings(
                    settings.CurrentIncomeEcu,
                    settings.BaseIncomeEcu,
                    accumulatedUje ?? settings.BaseUje,
                    settings.MaximumUje),
                definition.EconomyContext,
                definition.ConsumerCredits,
                initialCashBalance ?? definition.InitialCashBalance,
                initialDepositBalance ?? definition.InitialDepositBalance,
                definition.SourceSummary);
        }

        private static double? FirstNumber(JsonValue primary, JsonValue secondary, JsonValue tertiary, params string[] names)
        {
            return GetNumber(primary, names)
                   ?? GetNumber(secondary, names)
                   ?? GetNumber(tertiary, names);
        }

        private static double? FirstNumber(JsonValue primary, JsonValue secondary, params string[] names)
        {
            return GetNumber(primary, names)
                   ?? GetNumber(secondary, names);
        }

        private static string FirstString(JsonValue primary, JsonValue secondary, params string[] names)
        {
            var primaryValue = GetString(primary, names);

            if (!string.IsNullOrWhiteSpace(primaryValue))
            {
                return primaryValue;
            }

            var secondaryValue = GetString(secondary, names);
            return !string.IsNullOrWhiteSpace(secondaryValue)
                ? secondaryValue
                : string.Empty;
        }

        private static double? GetNumber(JsonValue node, params string[] names)
        {
            if (names == null)
            {
                return null;
            }

            foreach (var name in names)
            {
                var direct = node.FindFirstProperty(name).AsNullableNumber();

                if (direct.HasValue)
                {
                    return direct.Value;
                }

                var descendant = node.FindFirstDescendantProperty(name).AsNullableNumber();

                if (descendant.HasValue)
                {
                    return descendant.Value;
                }
            }

            return null;
        }

        private static bool? GetBoolean(JsonValue node, params string[] names)
        {
            if (names == null)
            {
                return null;
            }

            foreach (var name in names)
            {
                var direct = node.FindFirstProperty(name).AsNullableBoolean();

                if (direct.HasValue)
                {
                    return direct.Value;
                }

                var descendant = node.FindFirstDescendantProperty(name).AsNullableBoolean();

                if (descendant.HasValue)
                {
                    return descendant.Value;
                }
            }

            return null;
        }

        private static string GetString(JsonValue node, params string[] names)
        {
            if (names == null)
            {
                return string.Empty;
            }

            foreach (var name in names)
            {
                var direct = node.FindFirstProperty(name);

                if (direct.TryGetStringValue(out var directValue)
                    && !string.IsNullOrWhiteSpace(directValue))
                {
                    return directValue;
                }

                var descendant = node.FindFirstDescendantProperty(name);

                if (descendant.TryGetStringValue(out var descendantValue)
                    && !string.IsNullOrWhiteSpace(descendantValue))
                {
                    return descendantValue;
                }
            }

            return string.Empty;
        }
    }
}
