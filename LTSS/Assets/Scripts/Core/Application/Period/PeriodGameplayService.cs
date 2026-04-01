using System;
using System.Collections.Generic;
using System.Globalization;
using Game.Core.Application.Logging;
using Game.Core.Application.Navigation;
using Game.Core.Application.Networking;
using Game.Core.Application.Session;
using Game.Core.Application.State;
using Game.Core.Events;
using Game.Domain.GameFlow;
using UnityEngine;

namespace Game.Core.Application.Periods
{
    public sealed class PeriodGameplayService : IPeriodGameplayService
    {
        private readonly ISessionCoordinator _sessionCoordinator;
        private readonly ISessionPersistenceService _persistenceService;
        private readonly IPeriodRuntimeFactory _runtimeFactory;
        private readonly IPeriodCalculationEngine _calculationEngine;
        private readonly IPeriodCheckpointBuilder _checkpointBuilder;
        private readonly ICheckpointSender _checkpointSender;
        private readonly IGameSessionService _gameSessionService;
        private readonly IApplicationStateStore _stateStore;
        private readonly IPopupNavigationService _popupNavigation;
        private readonly IUserActionLogger _userActionLogger;
        private readonly IAppLogger _logger;
        private readonly IEventAggregator _eventAggregator;
        private readonly IJsonSerializer _serializer;

        private PeriodRuntimeState _current = PeriodRuntimeState.Empty;

        public PeriodRuntimeState Current => _current;

        public event Action<PeriodRuntimeState> Changed;

        public PeriodGameplayService(
            ISessionCoordinator sessionCoordinator,
            ISessionPersistenceService persistenceService,
            IPeriodRuntimeFactory runtimeFactory,
            IPeriodCalculationEngine calculationEngine,
            IPeriodCheckpointBuilder checkpointBuilder,
            ICheckpointSender checkpointSender,
            IGameSessionService gameSessionService,
            IApplicationStateStore stateStore,
            IPopupNavigationService popupNavigation,
            IUserActionLogger userActionLogger,
            IAppLogger logger,
            IEventAggregator eventAggregator,
            IJsonSerializer serializer)
        {
            _sessionCoordinator = sessionCoordinator;
            _persistenceService = persistenceService;
            _runtimeFactory = runtimeFactory;
            _calculationEngine = calculationEngine;
            _checkpointBuilder = checkpointBuilder;
            _checkpointSender = checkpointSender;
            _gameSessionService = gameSessionService;
            _stateStore = stateStore;
            _popupNavigation = popupNavigation;
            _userActionLogger = userActionLogger;
            _logger = logger;
            _eventAggregator = eventAggregator;
            _serializer = serializer;

            if (_sessionCoordinator != null)
            {
                _sessionCoordinator.RuntimeChanged += OnRuntimeChanged;
            }

            if (_stateStore != null)
            {
                _stateStore.StateChanged += OnApplicationStateChanged;
            }
        }

        public void ActivateCurrentPeriod()
        {
            var clientRuntime = _sessionCoordinator != null
                ? _sessionCoordinator.CurrentRuntime
                : ClientRuntimeState.Empty;

            if (clientRuntime == null || !clientRuntime.HasSession)
            {
                Publish(PeriodRuntimeState.Empty);
                return;
            }

            var targetPeriodNumber = clientRuntime.Bootstrap.Run != null && clientRuntime.Bootstrap.Run.CurrentPeriodNumber > 0
                ? clientRuntime.Bootstrap.Run.CurrentPeriodNumber
                : 1;

            Publish(new PeriodRuntimeState(
                clientRuntime.AuthenticatedRun.RunId,
                targetPeriodNumber,
                PeriodFlowState.LoadingData,
                _current.Definition,
                _current.Expenses,
                _current.AssetOperations,
                _current.Summary,
                AssetOperationDialogState.Closed,
                _current.IsCheckpointSubmitted,
                "Подготовка периода...",
                string.Empty,
                _current.SubmittedAtUtc,
                _current.HasPendingLocalChanges));

            if (_current.HasDefinition
                && string.Equals(_current.RunId, clientRuntime.AuthenticatedRun.RunId, StringComparison.Ordinal)
                && _current.PeriodNumber == targetPeriodNumber)
            {
                Publish(_current.FlowState == PeriodFlowState.None
                    ? _current.With(flowState: PeriodFlowState.PeriodActive)
                    : _current);
                return;
            }

            if (_runtimeFactory.TryRestore(clientRuntime, LoadPersistedPeriodSnapshot(), out var restoredState, out _))
            {
                Publish(restoredState.With(
                    flowState: restoredState.IsCheckpointSubmitted
                        ? PeriodFlowState.PeriodClosed
                        : PeriodFlowState.PeriodActive,
                    statusMessage: restoredState.IsCheckpointSubmitted
                        ? "Период уже сохранен."
                        : "Черновик периода восстановлен.",
                    assetDialog: AssetOperationDialogState.Closed));

                PersistCurrentState();
                _logger.Info($"Period state restored for run '{restoredState.RunId}' and period {restoredState.PeriodNumber}.");
                _userActionLogger.Log(
                    UserActionType.Interaction,
                    "period_restore_succeeded",
                    BuildPeriodMetadata(restoredState));
                return;
            }

            if (!_runtimeFactory.TryCreateNew(clientRuntime, out var createdState, out var error))
            {
                var failedState = PeriodRuntimeState.Empty.With(
                    flowState: PeriodFlowState.LoadingData,
                    statusMessage: string.Empty,
                    lastError: error ?? "Не удалось подготовить период.");
                Publish(failedState);
                _logger.Error(error);
                return;
            }

            createdState = createdState.With(flowState: PeriodFlowState.PeriodActive);
            Publish(createdState);
            PersistCurrentState();

            _logger.Info($"Period runtime created for run '{createdState.RunId}' and period {createdState.PeriodNumber}.");
            _userActionLogger.Log(
                UserActionType.Interaction,
                "period_initialized",
                BuildPeriodMetadata(createdState));
        }

        public void SetExpenseAmount(string expenseId, string rawAmount)
        {
            if (!_current.HasDefinition || !CanEditCurrentState() || string.IsNullOrWhiteSpace(expenseId))
            {
                return;
            }

            if (!TryParseAmount(rawAmount, out var amount))
            {
                return;
            }

            var currentExpenseState = FindExpenseState(expenseId);

            if (currentExpenseState == null)
            {
                return;
            }

            if (!CanAssignExpenseAmount(currentExpenseState, currentExpenseState.Source, amount, out var amountValidationMessage))
            {
                RejectExpenseMutation(amountValidationMessage);
                return;
            }

            var nextExpenses = new List<PeriodExpenseState>(_current.Expenses.Count);
            var hasChanges = false;

            foreach (var expenseState in _current.Expenses)
            {
                if (expenseState == null)
                {
                    continue;
                }

                if (string.Equals(expenseState.ExpenseId, expenseId, StringComparison.Ordinal))
                {
                    nextExpenses.Add(expenseState.With(amount: amount));
                    hasChanges = true;
                }
                else
                {
                    nextExpenses.Add(expenseState);
                }
            }

            if (!hasChanges)
            {
                return;
            }

            ApplyMutation(
                nextExpenses,
                _current.AssetOperations,
                _current.AssetDialog.IsOpen ? _current.AssetDialog : AssetOperationDialogState.Closed,
                PeriodFlowState.PeriodActive,
                string.Empty,
                string.Empty,
                true);
        }

        public void CycleExpenseSource(string expenseId)
        {
            if (!_current.HasDefinition || !CanEditCurrentState() || string.IsNullOrWhiteSpace(expenseId))
            {
                return;
            }

            var definition = FindExpenseDefinition(expenseId);

            if (definition == null || definition.AllowedSources == null || definition.AllowedSources.Count < 2)
            {
                return;
            }

            var nextExpenses = new List<PeriodExpenseState>(_current.Expenses.Count);

            foreach (var expenseState in _current.Expenses)
            {
                if (expenseState == null)
                {
                    continue;
                }

                if (!string.Equals(expenseState.ExpenseId, expenseId, StringComparison.Ordinal))
                {
                    nextExpenses.Add(expenseState);
                    continue;
                }

                var currentIndex = 0;

                for (var index = 0; index < definition.AllowedSources.Count; index++)
                {
                    if (definition.AllowedSources[index] == expenseState.Source)
                    {
                        currentIndex = index;
                        break;
                    }
                }

                var nextIndex = (currentIndex + 1) % definition.AllowedSources.Count;
                var nextSource = definition.AllowedSources[nextIndex];

                if (!CanAssignExpenseAmount(expenseState, nextSource, expenseState.Amount, out var sourceValidationMessage))
                {
                    RejectExpenseMutation(sourceValidationMessage);
                    return;
                }

                nextExpenses.Add(expenseState.With(source: nextSource));
            }

            ApplyMutation(
                nextExpenses,
                _current.AssetOperations,
                _current.AssetDialog.IsOpen ? _current.AssetDialog : AssetOperationDialogState.Closed,
                PeriodFlowState.PeriodActive,
                string.Empty,
                string.Empty,
                true);
        }

        public void OpenAssetDialog(string assetId, AssetOperationKind kind)
        {
            if (!_current.HasDefinition || !CanEditCurrentState() || _current.AssetDialog.IsOpen)
            {
                return;
            }

            var dialog = BuildAssetDialog(assetId, kind, FundsSourceType.Unknown);

            if (dialog == null || !dialog.IsOpen)
            {
                return;
            }

            _popupNavigation?.Push(Enums.PopupType.AssetOperation, "asset_operation_open");
            Publish(_current.With(
                assetDialog: dialog,
                statusMessage: string.Empty,
                lastError: string.Empty));
        }

        public void CycleAssetDialogSource()
        {
            if (_current == null || !CanEditCurrentState() || !_current.AssetDialog.IsOpen)
            {
                return;
            }

            var allowedSources = _current.AssetDialog.AllowedSources;

            if (allowedSources == null || allowedSources.Count < 2)
            {
                return;
            }

            var currentIndex = 0;

            for (var index = 0; index < allowedSources.Count; index++)
            {
                if (allowedSources[index] == _current.AssetDialog.SelectedSource)
                {
                    currentIndex = index;
                    break;
                }
            }

            var nextIndex = (currentIndex + 1) % allowedSources.Count;
            var rebuiltDialog = BuildAssetDialog(
                _current.AssetDialog.AssetId,
                _current.AssetDialog.Kind,
                allowedSources[nextIndex]);

            if (rebuiltDialog != null)
            {
                Publish(_current.With(assetDialog: rebuiltDialog, statusMessage: string.Empty));
            }
        }

        public void SubmitAssetDialog(string rawAmount)
        {
            Debug.Log($"[PeriodDebug] SubmitAssetDialog called. RawAmount='{rawAmount}'.");

            if (_current == null || !_current.AssetDialog.IsOpen)
            {
                Debug.Log("[PeriodDebug] SubmitAssetDialog aborted. Current runtime is null or asset dialog is closed.");
                return;
            }

            Debug.Log(
                $"[PeriodDebug] Dialog state. Asset='{_current.AssetDialog.AssetId}', Kind='{_current.AssetDialog.Kind}', Source='{_current.AssetDialog.SelectedSource}', MaxAmount={_current.AssetDialog.MaxAmount}, ExistingOperations={(_current.AssetOperations != null ? _current.AssetOperations.Count : 0)}.");

            if (_current.AssetDialog.MaxAmount <= 0.01d)
            {
                Debug.Log("[PeriodDebug] SubmitAssetDialog blocked. MaxAmount <= 0.");
                Publish(_current.With(statusMessage: "Сейчас нет доступных средств для этой операции."));
                return;
            }

            if (!TryParseAmount(rawAmount, out var amount) || amount <= 0d)
            {
                Debug.Log($"[PeriodDebug] SubmitAssetDialog blocked. Parsed amount is invalid: {amount}.");
                Publish(_current.With(statusMessage: "Введите корректную сумму."));
                return;
            }

            if (amount > _current.AssetDialog.MaxAmount + 0.01d)
            {
                Debug.Log($"[PeriodDebug] SubmitAssetDialog blocked. Amount {amount} exceeds max {_current.AssetDialog.MaxAmount}.");
                Publish(_current.With(statusMessage: "Сумма операции превышает доступный лимит."));
                return;
            }

            var nextOperations = new List<PeriodAssetOperationEntry>(_current.AssetOperations)
            {
                new PeriodAssetOperationEntry(
                    Guid.NewGuid().ToString("N"),
                    _current.AssetDialog.AssetId,
                    _current.AssetDialog.Kind,
                    _current.AssetDialog.SelectedSource,
                    amount,
                    DateTime.UtcNow.ToString("O"))
            };

            Debug.Log($"[PeriodDebug] SubmitAssetDialog passed validation. NextOperationCount={nextOperations.Count}.");

            ApplyMutation(
                _current.Expenses,
                nextOperations,
                AssetOperationDialogState.Closed,
                PeriodFlowState.PeriodActive,
                string.Empty,
                string.Empty,
                true);

            Debug.Log(
                $"[PeriodDebug] ApplyMutation completed for asset submit. NewOperationCount={(_current.AssetOperations != null ? _current.AssetOperations.Count : 0)}, Cash={_current.Summary.CashBalance}, Deposit={_current.Summary.DepositBalance}, Remaining={_current.Summary.RemainingToAllocate}.");

            _popupNavigation?.Pop("asset_operation_submit");
            Debug.Log("[PeriodDebug] PopupNavigation.Pop called for asset submit.");
        }

        public void CloseAssetDialog()
        {
            if (_current == null || !_current.AssetDialog.IsOpen)
            {
                return;
            }

            Publish(_current.With(assetDialog: AssetOperationDialogState.Closed, statusMessage: string.Empty));
            _popupNavigation?.Pop("asset_operation_close");
        }

        public void SubmitPeriod()
        {
            if (!_current.HasDefinition || _current.IsCheckpointSubmitted)
            {
                return;
            }

            Publish(_current.With(
                flowState: PeriodFlowState.PeriodValidation,
                statusMessage: "Проверка периода...",
                lastError: string.Empty));

            var summary = _calculationEngine.Recalculate(_current.Definition, _current.Expenses, _current.AssetOperations);

            if (!summary.CanComplete)
            {
                Publish(_current.With(
                    flowState: PeriodFlowState.PeriodValidation,
                    summary: summary,
                    statusMessage: FirstBlockingValidationMessage(summary),
                    lastError: string.Empty,
                    hasPendingLocalChanges: true));
                PersistCurrentState();
                return;
            }

            if (!_checkpointBuilder.TryBuild(_current.With(summary: summary), out var request, out var buildError))
            {
                Publish(_current.With(
                    flowState: PeriodFlowState.PeriodValidation,
                    summary: summary,
                    statusMessage: buildError,
                    lastError: buildError,
                    hasPendingLocalChanges: true));
                PersistCurrentState();
                return;
            }

            Publish(_current.With(
                flowState: PeriodFlowState.PeriodClosing,
                summary: summary,
                statusMessage: "Фиксация периода...",
                lastError: string.Empty,
                hasPendingLocalChanges: true));
            PersistCurrentState();

            var clientRuntime = _sessionCoordinator != null
                ? _sessionCoordinator.CurrentRuntime
                : ClientRuntimeState.Empty;

            if (clientRuntime == null || !clientRuntime.HasSession)
            {
                Publish(_current.With(
                    flowState: PeriodFlowState.PeriodValidation,
                    summary: summary,
                    statusMessage: "Сессия недоступна. Сохраните состояние и войдите снова.",
                    lastError: "Сессия недоступна.",
                    hasPendingLocalChanges: true));
                PersistCurrentState();
                return;
            }

            Publish(_current.With(
                flowState: PeriodFlowState.PeriodCheckpointSubmitting,
                summary: summary,
                statusMessage: "Отправка checkpoint...",
                lastError: string.Empty,
                hasPendingLocalChanges: true));
            PersistCurrentState();

            _logger.Info($"Checkpoint submit started for run '{clientRuntime.AuthenticatedRun.RunId}', period {_current.PeriodNumber}.");
            _userActionLogger.Log(
                UserActionType.Interaction,
                "period_checkpoint_submit_started",
                BuildPeriodMetadata(_current));

            _checkpointSender.Send(
                clientRuntime.AuthenticatedRun.RunId,
                clientRuntime.AuthToken.Token,
                request,
                response =>
                {
                    if (response != null && response.IsSuccess)
                    {
                        var submittedAt = DateTime.UtcNow.ToString("O");

                        Publish(_current.With(
                            flowState: PeriodFlowState.PeriodClosed,
                            isCheckpointSubmitted: true,
                            statusMessage: "Период сохранен.",
                            lastError: string.Empty,
                            submittedAtUtc: submittedAt,
                            hasPendingLocalChanges: false));
                        PersistCurrentState();

                        _logger.Info($"Checkpoint submit succeeded for run '{clientRuntime.AuthenticatedRun.RunId}', period {_current.PeriodNumber}.");
                        _userActionLogger.Log(
                            UserActionType.Interaction,
                            "period_checkpoint_submit_succeeded",
                            BuildPeriodMetadata(_current));

                        var totalPeriods = ResolveTotalPeriodCount(clientRuntime);
                        var hasNextPeriod = totalPeriods > 0 && _current.PeriodNumber < totalPeriods;

                        if (hasNextPeriod)
                        {
                            var nextPeriodNumber = _current.PeriodNumber + 1;
                            _sessionCoordinator?.UpdateLocalRunProgress(nextPeriodNumber, RunLifecycleStatus.InProgress);
                            ActivateCurrentPeriod();
                            return;
                        }

                        var completionMessage = "Все периоды завершены. Далее будет пост-экспериментальный этап.";
                        Publish(_current.With(
                            statusMessage: completionMessage,
                            lastError: string.Empty));
                        PersistCurrentState();
                        _sessionCoordinator?.CompleteRunLocally(completionMessage, "run_completed_after_last_period");
                        return;
                    }

                    var errorMessage = response == null || string.IsNullOrWhiteSpace(response.Error)
                        ? "Не удалось отправить checkpoint."
                        : $"Не удалось отправить checkpoint. {response.Error}";

                    Publish(_current.With(
                        flowState: PeriodFlowState.PeriodValidation,
                        statusMessage: errorMessage,
                        lastError: errorMessage,
                        hasPendingLocalChanges: true));
                    PersistCurrentState();

                    _logger.Warning(errorMessage);
                    _userActionLogger.Log(
                        UserActionType.Interaction,
                        "period_checkpoint_submit_failed",
                        BuildPeriodMetadata(_current));
                });
        }

        public void ClearRuntime()
        {
            _current = PeriodRuntimeState.Empty;
            _persistenceService?.ClearPeriodSnapshot();
            Changed?.Invoke(_current);
            _eventAggregator?.Publish(new EventsProvider.PeriodRuntimeChangedEvent(_current));
        }

        private PersistedPeriodSnapshot LoadPersistedPeriodSnapshot()
        {
            return _persistenceService != null && _persistenceService.TryLoadPeriodSnapshot(out var snapshot)
                ? snapshot
                : null;
        }

        private void ApplyMutation(
            IReadOnlyList<PeriodExpenseState> expenses,
            IReadOnlyList<PeriodAssetOperationEntry> operations,
            AssetOperationDialogState dialog,
            PeriodFlowState flowState,
            string statusMessage,
            string lastError,
            bool hasPendingLocalChanges)
        {
            var summary = _calculationEngine.Recalculate(_current.Definition, expenses, operations);

            Publish(_current.With(
                flowState: flowState,
                expenses: expenses,
                assetOperations: operations,
                summary: summary,
                assetDialog: dialog ?? AssetOperationDialogState.Closed,
                statusMessage: statusMessage,
                lastError: lastError,
                hasPendingLocalChanges: hasPendingLocalChanges));
            PersistCurrentState();
        }

        private void PersistCurrentState()
        {
            if (_persistenceService == null || !_current.HasDefinition)
            {
                return;
            }

            var dto = new PeriodRuntimeSnapshotDto
            {
                runId = _current.RunId,
                periodNumber = _current.PeriodNumber,
                flowState = PeriodContractMapper.ToPeriodFlowStateCode(_current.FlowState),
                expenses = BuildExpenseSnapshots(_current.Expenses),
                assetOperations = BuildOperationSnapshots(_current.AssetOperations),
                isCheckpointSubmitted = _current.IsCheckpointSubmitted,
                statusMessage = _current.StatusMessage,
                lastError = _current.LastError,
                submittedAtUtc = _current.SubmittedAtUtc,
                hasPendingLocalChanges = _current.HasPendingLocalChanges
            };

            _persistenceService.SavePeriodSnapshot(
                _current.RunId,
                _current.PeriodNumber,
                dto.flowState,
                _serializer.Serialize(dto),
                _current.IsCheckpointSubmitted);
        }

        private static PeriodExpenseStateSnapshotDto[] BuildExpenseSnapshots(IReadOnlyList<PeriodExpenseState> expenses)
        {
            if (expenses == null || expenses.Count == 0)
            {
                return Array.Empty<PeriodExpenseStateSnapshotDto>();
            }

            var result = new PeriodExpenseStateSnapshotDto[expenses.Count];

            for (var index = 0; index < expenses.Count; index++)
            {
                var expense = expenses[index];
                result[index] = expense == null
                    ? null
                    : new PeriodExpenseStateSnapshotDto
                    {
                        expenseId = expense.ExpenseId,
                        amount = expense.Amount,
                        source = PeriodContractMapper.ToFundsSourceCode(expense.Source)
                    };
            }

            return result;
        }

        private static PeriodAssetOperationSnapshotDto[] BuildOperationSnapshots(IReadOnlyList<PeriodAssetOperationEntry> operations)
        {
            if (operations == null || operations.Count == 0)
            {
                return Array.Empty<PeriodAssetOperationSnapshotDto>();
            }

            var result = new PeriodAssetOperationSnapshotDto[operations.Count];

            for (var index = 0; index < operations.Count; index++)
            {
                var operation = operations[index];
                result[index] = operation == null
                    ? null
                    : new PeriodAssetOperationSnapshotDto
                    {
                        operationId = operation.OperationId,
                        assetId = operation.AssetId,
                        kind = PeriodContractMapper.ToAssetOperationKindCode(operation.Kind),
                        source = PeriodContractMapper.ToFundsSourceCode(operation.Source),
                        amount = operation.Amount,
                        createdAtUtc = operation.CreatedAtUtc
                    };
            }

            return result;
        }

        private AssetOperationDialogState BuildAssetDialog(
            string assetId,
            AssetOperationKind kind,
            FundsSourceType preferredSource)
        {
            if (string.IsNullOrWhiteSpace(assetId) || kind == AssetOperationKind.None)
            {
                return null;
            }

            var asset = FindAssetBalance(assetId);

            if (asset == null)
            {
                return null;
            }

            IReadOnlyList<FundsSourceType> allowedSources;

            if (kind == AssetOperationKind.Withdraw)
            {
                if (asset.AssetType != PeriodAssetType.Deposit || asset.CurrentAmount <= 0d)
                {
                    return null;
                }

                allowedSources = new[] { FundsSourceType.Deposit };
            }
            else if (asset.AssetType == PeriodAssetType.Cash)
            {
                allowedSources = new[] { FundsSourceType.CurrentIncome };
            }
            else
            {
                var definition = FindAssetDefinition(assetId);
                allowedSources = definition != null && definition.AllowedDepositSources != null && definition.AllowedDepositSources.Count > 0
                    ? definition.AllowedDepositSources
                    : new[] { FundsSourceType.CurrentIncome };
            }

            var selectedSource = preferredSource != FundsSourceType.Unknown
                ? preferredSource
                : allowedSources[0];
            var maxAmount = ResolveAssetDialogMaxAmount(asset.AssetType, kind, selectedSource);

            return new AssetOperationDialogState(
                true,
                asset.AssetId,
                asset.Title,
                kind,
                allowedSources,
                selectedSource,
                maxAmount > 0d ? maxAmount : 0d,
                maxAmount,
                kind == AssetOperationKind.Withdraw
                    ? $"Вывести из актива: {asset.Title}"
                    : $"Операция с активом: {asset.Title}",
                kind == AssetOperationKind.Withdraw
                    ? "Средства будут переведены в наличные."
                    : $"Доступный лимит: {maxAmount.ToString("0.##", CultureInfo.InvariantCulture)}");
        }

        private double ResolveAssetDialogMaxAmount(
            PeriodAssetType assetType,
            AssetOperationKind kind,
            FundsSourceType source)
        {
            var summary = _current.Summary ?? PeriodCalculationSummary.Empty;

            if (kind == AssetOperationKind.Withdraw)
            {
                return assetType == PeriodAssetType.Deposit
                    ? Math.Max(0d, summary.DepositBalance)
                    : 0d;
            }

            if (assetType == PeriodAssetType.Cash)
            {
                return source == FundsSourceType.CurrentIncome
                    ? Math.Max(0d, summary.RemainingToAllocate)
                    : 0d;
            }

            switch (source)
            {
                case FundsSourceType.CurrentIncome:
                    return Math.Max(0d, summary.RemainingToAllocate);
                case FundsSourceType.Cash:
                    return Math.Max(0d, summary.CashBalance);
                default:
                    return 0d;
            }
        }

        private PeriodExpenseDefinition FindExpenseDefinition(string expenseId)
        {
            if (!_current.HasDefinition)
            {
                return null;
            }

            foreach (var definition in _current.Definition.ExpenseDefinitions)
            {
                if (definition != null && string.Equals(definition.Id, expenseId, StringComparison.Ordinal))
                {
                    return definition;
                }
            }

            return null;
        }

        private PeriodExpenseState FindExpenseState(string expenseId)
        {
            if (!_current.HasDefinition || string.IsNullOrWhiteSpace(expenseId))
            {
                return null;
            }

            foreach (var expenseState in _current.Expenses)
            {
                if (expenseState != null && string.Equals(expenseState.ExpenseId, expenseId, StringComparison.Ordinal))
                {
                    return expenseState;
                }
            }

            return null;
        }

        private bool CanAssignExpenseAmount(
            PeriodExpenseState expenseState,
            FundsSourceType targetSource,
            double targetAmount,
            out string validationMessage)
        {
            validationMessage = string.Empty;

            if (expenseState == null || targetAmount < 0d)
            {
                validationMessage = "Сумма расхода должна быть неотрицательной.";
                return false;
            }

            var definition = FindExpenseDefinition(expenseState.ExpenseId);

            if (definition == null)
            {
                return true;
            }

            var availableAmount = ResolveAvailableAmountForExpense(expenseState, targetSource);

            if (targetAmount <= availableAmount + 0.01d)
            {
                return true;
            }

            validationMessage = BuildExpenseSourceValidationMessage(definition, targetSource, availableAmount);
            return false;
        }

        private double ResolveAvailableAmountForExpense(PeriodExpenseState expenseState, FundsSourceType targetSource)
        {
            var summary = _current.Summary ?? PeriodCalculationSummary.Empty;
            var currentAmount = expenseState != null ? Math.Max(0d, expenseState.Amount) : 0d;
            var currentSource = expenseState != null ? expenseState.Source : FundsSourceType.Unknown;

            switch (targetSource)
            {
                case FundsSourceType.CurrentIncome:
                    return Math.Max(0d, summary.RemainingToAllocate + (currentSource == FundsSourceType.CurrentIncome ? currentAmount : 0d));
                case FundsSourceType.Cash:
                    return Math.Max(0d, summary.CashBalance + (currentSource == FundsSourceType.Cash ? currentAmount : 0d));
                case FundsSourceType.Deposit:
                    return Math.Max(0d, summary.DepositBalance + (currentSource == FundsSourceType.Deposit ? currentAmount : 0d));
                default:
                    return 0d;
            }
        }

        private string BuildExpenseSourceValidationMessage(
            PeriodExpenseDefinition definition,
            FundsSourceType source,
            double availableAmount)
        {
            var title = definition != null && !string.IsNullOrWhiteSpace(definition.Title)
                ? $"«{definition.Title}»"
                : "этой статьи";
            var availableText = availableAmount.ToString("0.##", CultureInfo.InvariantCulture);

            switch (source)
            {
                case FundsSourceType.Cash:
                    return $"Для {title} не хватает наличных. Доступно: {availableText} ₽.";
                case FundsSourceType.Deposit:
                    return $"Для {title} не хватает средств на депозите. Доступно: {availableText} ₽.";
                case FundsSourceType.CurrentIncome:
                default:
                    return $"Для {title} не хватает располагаемого дохода. Осталось распределить: {availableText} ₽.";
            }
        }

        private void RejectExpenseMutation(string message)
        {
            Publish(_current.With(
                statusMessage: string.IsNullOrWhiteSpace(message)
                    ? "Изменение расхода недоступно."
                    : message,
                lastError: string.Empty));
        }

        private PeriodAssetDefinition FindAssetDefinition(string assetId)
        {
            if (!_current.HasDefinition)
            {
                return null;
            }

            foreach (var definition in _current.Definition.AssetDefinitions)
            {
                if (definition != null && string.Equals(definition.Id, assetId, StringComparison.Ordinal))
                {
                    return definition;
                }
            }

            return null;
        }

        private PeriodAssetBalance FindAssetBalance(string assetId)
        {
            var summary = _current.Summary ?? PeriodCalculationSummary.Empty;

            foreach (var asset in summary.AssetBalances)
            {
                if (asset != null && string.Equals(asset.AssetId, assetId, StringComparison.Ordinal))
                {
                    return asset;
                }
            }

            return null;
        }

        private static bool TryParseAmount(string rawAmount, out double amount)
        {
            amount = 0d;
            var normalized = (rawAmount ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(normalized))
            {
                return true;
            }

            normalized = normalized.Replace(',', '.');

            return double.TryParse(
                normalized,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out amount)
                && amount >= 0d;
        }

        private static string FirstBlockingValidationMessage(PeriodCalculationSummary summary)
        {
            if (summary == null || summary.ValidationIssues == null)
            {
                return "Период пока нельзя завершить.";
            }

            foreach (var issue in summary.ValidationIssues)
            {
                if (issue != null && issue.IsBlocking && !string.IsNullOrWhiteSpace(issue.Message))
                {
                    return issue.Message;
                }
            }

            return "Период пока нельзя завершить.";
        }

        private static int ResolveTotalPeriodCount(ClientRuntimeState clientRuntime)
        {
            if (clientRuntime == null || !clientRuntime.HasSession)
            {
                return 0;
            }

            return clientRuntime.Bootstrap != null
                   && clientRuntime.Bootstrap.Session != null
                   && clientRuntime.Bootstrap.Session.SessionConfig != null
                   && clientRuntime.Bootstrap.Session.SessionConfig.PeriodCount.HasValue
                ? Math.Max(0, clientRuntime.Bootstrap.Session.SessionConfig.PeriodCount.Value)
                : 0;
        }

        private Dictionary<string, string> BuildPeriodMetadata(PeriodRuntimeState state)
        {
            var metadata = new Dictionary<string, string>();

            if (state == null)
            {
                return metadata;
            }

            if (!string.IsNullOrWhiteSpace(state.RunId))
            {
                metadata["runId"] = state.RunId;
            }

            metadata["period"] = state.PeriodNumber.ToString();
            metadata["flow"] = PeriodContractMapper.ToPeriodFlowStateCode(state.FlowState);
            metadata["submitted"] = state.IsCheckpointSubmitted.ToString();
            return metadata;
        }

        private void Publish(PeriodRuntimeState nextState)
        {
            _current = nextState ?? PeriodRuntimeState.Empty;

            if (_current.HasDefinition)
            {
                _gameSessionService?.SetStage(GameFlowStage.Gameplay);
                _gameSessionService?.SetPhase(PeriodContractMapper.ToGameFlowPhase(_current.FlowState));
            }

            Changed?.Invoke(_current);
            _eventAggregator?.Publish(new EventsProvider.PeriodRuntimeChangedEvent(_current));
        }

        private void OnRuntimeChanged(ClientRuntimeState runtimeState)
        {
            if (runtimeState == null || !runtimeState.HasSession)
            {
                ClearRuntime();
                return;
            }

            if (_current.HasDefinition
                && !string.Equals(_current.RunId, runtimeState.AuthenticatedRun.RunId, StringComparison.Ordinal))
            {
                ClearRuntime();
            }
        }

        private bool CanEditCurrentState()
        {
            return _current != null
                   && !_current.IsCheckpointSubmitted
                   && _current.FlowState != PeriodFlowState.PeriodCheckpointSubmitting
                   && _current.FlowState != PeriodFlowState.PeriodClosed;
        }

        private void OnApplicationStateChanged(ApplicationStateSnapshot snapshot)
        {
            if (_current == null || !_current.AssetDialog.IsOpen || snapshot == null)
            {
                return;
            }

            var popupIsPresent = false;

            foreach (var popup in snapshot.PopupStack)
            {
                if (popup != null && popup.Type == Enums.PopupType.AssetOperation)
                {
                    popupIsPresent = true;
                    break;
                }
            }

            if (!popupIsPresent)
            {
                Publish(_current.With(assetDialog: AssetOperationDialogState.Closed, statusMessage: string.Empty));
            }
        }
    }
}
