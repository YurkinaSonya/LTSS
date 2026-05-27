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
        private string _pendingCarryOverRunId = string.Empty;
        private int _pendingCarryOverTargetPeriodNumber;
        private double _pendingCarryOverCashBalance;
        private double _pendingCarryOverDepositBalance;
        private double _pendingCarryOverAccumulatedUje;

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
            var previousState = _current;

            if (clientRuntime == null || !clientRuntime.HasSession)
            {
                Publish(PeriodRuntimeState.Empty);
                return;
            }

            var targetPeriodNumber = clientRuntime.Bootstrap.Run != null && clientRuntime.Bootstrap.Run.CurrentPeriodNumber > 0
                ? clientRuntime.Bootstrap.Run.CurrentPeriodNumber
                : 1;

            if (!string.IsNullOrWhiteSpace(_pendingCarryOverRunId)
                && string.Equals(_pendingCarryOverRunId, clientRuntime.AuthenticatedRun.RunId, StringComparison.Ordinal)
                && _pendingCarryOverTargetPeriodNumber > targetPeriodNumber)
            {
                targetPeriodNumber = _pendingCarryOverTargetPeriodNumber;
            }

            if (previousState.HasDefinition
                && string.Equals(previousState.RunId, clientRuntime.AuthenticatedRun.RunId, StringComparison.Ordinal)
                && previousState.PeriodNumber == targetPeriodNumber
                && (previousState.FlowState == PeriodFlowState.PeriodIntro
                    || previousState.FlowState == PeriodFlowState.PeriodActive
                    || previousState.FlowState == PeriodFlowState.PeriodValidation))
            {
                Publish(previousState.FlowState == PeriodFlowState.None
                    ? previousState.With(flowState: PeriodFlowState.PeriodActive)
                    : previousState);
                return;
            }

            Publish(new PeriodRuntimeState(
                clientRuntime.AuthenticatedRun.RunId,
                targetPeriodNumber,
                PeriodFlowState.LoadingData,
                previousState.Definition,
                previousState.Expenses,
                previousState.AssetOperations,
                previousState.Summary,
                AssetOperationDialogState.Closed,
                previousState.IsCheckpointSubmitted,
                "Подготовка периода...",
                string.Empty,
                previousState.SubmittedAtUtc,
                previousState.HasPendingLocalChanges));

            if (_runtimeFactory.TryRestore(clientRuntime, LoadPersistedPeriodSnapshot(), out var restoredState, out _))
            {
                restoredState = TryApplyCarryOver(restoredState);
                Publish(restoredState.With(
                    flowState: restoredState.IsCheckpointSubmitted
                        ? PeriodFlowState.PeriodClosed
                        : PeriodFlowState.PeriodActive,
                    statusMessage: restoredState.IsCheckpointSubmitted
                        ? "Период уже сохранён."
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
            createdState = TryApplyCarryOver(createdState);
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

            if (!NumericInputParser.TryParseNonNegativeAmount(rawAmount, out var amount))
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

        public void ApplyRequiredExpenseAmount(string expenseId)
        {
            if (!_current.HasDefinition || !CanEditCurrentState() || string.IsNullOrWhiteSpace(expenseId))
            {
                return;
            }

            var definition = FindExpenseDefinition(expenseId);

            if (!TryResolveQuickApplyAmount(definition, out var targetAmount))
            {
                return;
            }

            SetExpenseAmount(
                expenseId,
                targetAmount.ToString("0.##", CultureInfo.InvariantCulture));
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

        public void SetExpenseSource(string expenseId, FundsSourceType source)
        {
            if (!_current.HasDefinition || !CanEditCurrentState() || string.IsNullOrWhiteSpace(expenseId))
            {
                return;
            }

            var definition = FindExpenseDefinition(expenseId);

            if (definition == null || definition.AllowedSources == null || definition.AllowedSources.Count == 0)
            {
                return;
            }

            var sourceAllowed = false;

            foreach (var allowedSource in definition.AllowedSources)
            {
                if (allowedSource == source)
                {
                    sourceAllowed = true;
                    break;
                }
            }

            if (!sourceAllowed)
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

                if (expenseState.Source == source)
                {
                    nextExpenses.Add(expenseState);
                    continue;
                }

                if (!CanAssignExpenseAmount(expenseState, source, expenseState.Amount, out var sourceValidationMessage))
                {
                    RejectExpenseMutation(sourceValidationMessage);
                    return;
                }

                nextExpenses.Add(expenseState.With(source: source));
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

            var assetBalance = FindAssetBalance(assetId);

            if (assetBalance != null
                && assetBalance.AssetType == PeriodAssetType.Apartment
                && kind == AssetOperationKind.Withdraw)
            {
                OpenApartmentSaleDialog();
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

        public void OpenConsumerCreditDialog()
        {
            if (!_current.HasDefinition
                || !CanEditCurrentState()
                || !IsConsumerCreditAvailable(_current.Definition))
            {
                return;
            }

            _popupNavigation?.Push(Enums.PopupType.ConsumerCredit, "consumer_credit_open");
            Publish(_current.With(statusMessage: string.Empty, lastError: string.Empty));
        }

        public void OpenApartmentPurchaseDialog()
        {
            if (!_current.HasDefinition
                || !CanEditCurrentState()
                || !IsMortgageAvailable(_current.Definition)
                || HasOwnedResidence(_current.Definition))
            {
                return;
            }

            _popupNavigation?.Push(Enums.PopupType.ApartmentPurchase, "apartment_purchase_open");
            Publish(_current.With(statusMessage: string.Empty, lastError: string.Empty));
        }

        public void OpenApartmentSaleDialog()
        {
            if (!_current.HasDefinition
                || !CanEditCurrentState()
                || !HasOwnedResidence(_current.Definition))
            {
                return;
            }

            _popupNavigation?.Push(Enums.PopupType.ApartmentSale, "apartment_sale_open");
            Publish(_current.With(statusMessage: string.Empty, lastError: string.Empty));
        }

        public void OpenMortgageDialog()
        {
            if (!_current.HasDefinition
                || !CanEditCurrentState()
                || !IsMortgageAvailable(_current.Definition)
                || HasOwnedResidence(_current.Definition)
                || HasActiveMortgage(_current.Definition.ConsumerCredits))
            {
                return;
            }

            _popupNavigation?.Push(Enums.PopupType.Mortgage, "mortgage_open");
            Publish(_current.With(statusMessage: string.Empty, lastError: string.Empty));
        }

        public void OpenPdsDialog()
        {
            if (!_current.HasDefinition
                || !CanEditCurrentState()
                || !IsPdsAvailable(_current.Definition))
            {
                return;
            }

            var popupType = RequiresManualPdsEnrollment() && _current.Definition.PdsAccount == null
                ? Enums.PopupType.PdsCalculator
                : Enums.PopupType.Pds;

            _popupNavigation?.Push(popupType, "pds_open");
            Publish(_current.With(statusMessage: string.Empty, lastError: string.Empty));
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

        public void SetAssetDialogSource(FundsSourceType source)
        {
            if (_current == null || !CanEditCurrentState() || !_current.AssetDialog.IsOpen)
            {
                return;
            }

            var allowedSources = _current.AssetDialog.AllowedSources;

            if (allowedSources == null || allowedSources.Count == 0)
            {
                return;
            }

            var sourceAllowed = false;

            foreach (var allowedSource in allowedSources)
            {
                if (allowedSource == source)
                {
                    sourceAllowed = true;
                    break;
                }
            }

            if (!sourceAllowed || _current.AssetDialog.SelectedSource == source)
            {
                return;
            }

            var rebuiltDialog = BuildAssetDialog(
                _current.AssetDialog.AssetId,
                _current.AssetDialog.Kind,
                source);

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

            if (!NumericInputParser.TryParseNonNegativeAmount(rawAmount, out var amount) || amount <= 0d)
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

        public bool TrySubmitConsumerCredit(string rawAmount, out string errorMessage)
        {
            errorMessage = string.Empty;

            if (!_current.HasDefinition || !CanEditCurrentState())
            {
                errorMessage = "Период недоступен для оформления кредита.";
                return false;
            }

            if (!IsConsumerCreditAvailable(_current.Definition))
            {
                errorMessage = "Потребительский кредит пока недоступен.";
                return false;
            }

            if (!NumericInputParser.TryParseNonNegativeAmount(rawAmount, out var principal) || principal <= 0d)
            {
                errorMessage = "Введите корректную сумму кредита.";
                return false;
            }

            var definition = _current.Definition;
            var ratePercent = definition.EconomyContext != null
                ? definition.EconomyContext.CreditRate
                : null;
            var periodicPayment = ConsumerCreditMath.CalculateAnnuityPayment(principal, ratePercent);
            var creditPotential = ConsumerCreditMath.CalculateCreditPotential(definition);

            if (periodicPayment > creditPotential + 0.01d)
            {
                errorMessage = $"Платёж по кредиту превышает кредитный потенциал ({EcuFormatter.FormatAmount(creditPotential)}).";
                return false;
            }

            var nextCredits = new List<ConsumerCreditContractRuntime>(definition.ConsumerCredits ?? Array.Empty<ConsumerCreditContractRuntime>())
            {
                new ConsumerCreditContractRuntime(
                    ConsumerCreditMath.ConsumerCreditKind,
                    Guid.NewGuid().ToString("N"),
                    _current.PeriodNumber,
                    principal,
                    principal,
                    periodicPayment,
                    ratePercent ?? 0d,
                    ConsumerCreditMath.DefaultTermPeriods)
            };

            var nextDefinition = CloneDefinition(
                definition,
                consumerCredits: nextCredits,
                initialCashBalance: definition.InitialCashBalance + principal);
            var nextSummary = _calculationEngine.Recalculate(nextDefinition, _current.Expenses, _current.AssetOperations);
            var nextState = new PeriodRuntimeState(
                _current.RunId,
                _current.PeriodNumber,
                PeriodFlowState.PeriodActive,
                nextDefinition,
                _current.Expenses,
                _current.AssetOperations,
                nextSummary,
                AssetOperationDialogState.Closed,
                _current.IsCheckpointSubmitted,
                "Кредит оформлен.",
                string.Empty,
                _current.SubmittedAtUtc,
                true);

            Publish(nextState);
            PersistCurrentState();
            return true;
        }

        public bool TryBuyApartment(out string errorMessage)
        {
            errorMessage = string.Empty;

            if (!_current.HasDefinition || !CanEditCurrentState())
            {
                errorMessage = "Период недоступен для покупки квартиры.";
                return false;
            }

            if (!IsMortgageAvailable(_current.Definition))
            {
                errorMessage = "Покупка квартиры пока недоступна.";
                return false;
            }

            if (HasOwnedResidence(_current.Definition))
            {
                errorMessage = "Квартира уже куплена.";
                return false;
            }

            var summary = _current.Summary ?? PeriodCalculationSummary.Empty;
            var apartmentCost = ConsumerCreditMath.CalculateApartmentCost(_current.Definition);

            if (summary.CashBalance + 0.01d < apartmentCost)
            {
                errorMessage = $"Для покупки нужно {EcuFormatter.FormatAmount(apartmentCost)} наличными.";
                return false;
            }

            var definition = _current.Definition;
            var nextResidence = BuildResidenceOwnership(
                _current.PeriodNumber,
                definition.EconomyContext,
                ConsumerCreditMath.DirectApartmentPurchaseMode);
            var nextDefinition = BuildStateAwareDefinition(
                definition,
                residenceOwnership: nextResidence,
                replaceResidenceOwnership: true,
                initialCashBalance: definition.InitialCashBalance - apartmentCost);
            var nextExpenses = BuildResidenceAwareExpenseStates(_current.Expenses, nextDefinition.ExpenseDefinitions);
            var nextSummary = _calculationEngine.Recalculate(nextDefinition, nextExpenses, _current.AssetOperations);
            var nextState = new PeriodRuntimeState(
                _current.RunId,
                _current.PeriodNumber,
                PeriodFlowState.PeriodActive,
                nextDefinition,
                nextExpenses,
                _current.AssetOperations,
                nextSummary,
                AssetOperationDialogState.Closed,
                _current.IsCheckpointSubmitted,
                "Квартира куплена.",
                string.Empty,
                _current.SubmittedAtUtc,
                true);

            Publish(nextState);
            PersistCurrentState();
            return true;
        }

        public bool TrySubmitMortgage(out string errorMessage)
        {
            errorMessage = string.Empty;

            if (!_current.HasDefinition || !CanEditCurrentState())
            {
                errorMessage = "Период недоступен для оформления ипотеки.";
                return false;
            }

            if (!IsMortgageAvailable(_current.Definition))
            {
                errorMessage = "Ипотека пока недоступна.";
                return false;
            }

            if (HasActiveMortgage(_current.Definition.ConsumerCredits))
            {
                errorMessage = "Активная ипотека уже оформлена.";
                return false;
            }

            if (HasOwnedResidence(_current.Definition))
            {
                errorMessage = "Квартира уже куплена.";
                return false;
            }

            var summary = _current.Summary ?? PeriodCalculationSummary.Empty;
            var downPayment = ConsumerCreditMath.CalculateMortgageDownPayment(_current.Definition);
            var mortgagePrincipal = ConsumerCreditMath.CalculateMortgagePrincipal(_current.Definition);

            if (summary.CashBalance + 0.01d < downPayment)
            {
                errorMessage = $"Для первоначального взноса нужно {EcuFormatter.FormatAmount(downPayment)} наличными.";
                return false;
            }

            var definition = _current.Definition;
            var ratePercent = definition.EconomyContext != null
                ? definition.EconomyContext.MortgageRate
                : null;
            var periodicPayment = ConsumerCreditMath.CalculateAnnuityPayment(
                mortgagePrincipal,
                ratePercent,
                ConsumerCreditMath.MortgageTermPeriods);
            var nextCredits = new List<ConsumerCreditContractRuntime>(definition.ConsumerCredits ?? Array.Empty<ConsumerCreditContractRuntime>())
            {
                new ConsumerCreditContractRuntime(
                    ConsumerCreditMath.MortgageKind,
                    Guid.NewGuid().ToString("N"),
                    _current.PeriodNumber,
                    mortgagePrincipal,
                    mortgagePrincipal,
                    periodicPayment,
                    ratePercent ?? 0d,
                    ConsumerCreditMath.MortgageTermPeriods)
            };

            var nextResidence = BuildResidenceOwnership(
                _current.PeriodNumber,
                definition.EconomyContext,
                ConsumerCreditMath.MortgageApartmentPurchaseMode);
            var nextDefinition = BuildStateAwareDefinition(
                definition,
                consumerCredits: nextCredits,
                residenceOwnership: nextResidence,
                replaceResidenceOwnership: true,
                initialCashBalance: definition.InitialCashBalance - downPayment);
            var nextExpenses = BuildResidenceAwareExpenseStates(_current.Expenses, nextDefinition.ExpenseDefinitions);
            var nextSummary = _calculationEngine.Recalculate(nextDefinition, nextExpenses, _current.AssetOperations);
            var nextState = new PeriodRuntimeState(
                _current.RunId,
                _current.PeriodNumber,
                PeriodFlowState.PeriodActive,
                nextDefinition,
                nextExpenses,
                _current.AssetOperations,
                nextSummary,
                AssetOperationDialogState.Closed,
                _current.IsCheckpointSubmitted,
                "Ипотека оформлена.",
                string.Empty,
                _current.SubmittedAtUtc,
                true);

            Publish(nextState);
            PersistCurrentState();
            return true;
        }

        public bool TrySellApartment(out string errorMessage)
        {
            errorMessage = string.Empty;

            if (!_current.HasDefinition || !CanEditCurrentState())
            {
                errorMessage = "Период недоступен для продажи квартиры.";
                return false;
            }

            if (!HasOwnedResidence(_current.Definition))
            {
                errorMessage = "Квартира не куплена.";
                return false;
            }

            SellApartment();
            return true;
        }

        public bool TryActivatePds(
            string rawContributionAmount,
            string rawPensionTransferAmount,
            out string errorMessage)
        {
            return TryApplyPdsMutation(
                rawContributionAmount,
                rawPensionTransferAmount,
                true,
                out errorMessage);
        }

        public bool TrySubmitPds(
            string rawContributionAmount,
            string rawPensionTransferAmount,
            out string errorMessage)
        {
            return TryApplyPdsMutation(
                rawContributionAmount,
                rawPensionTransferAmount,
                false,
                out errorMessage);
        }

        private bool TryApplyPdsMutation(
            string rawContributionAmount,
            string rawPensionTransferAmount,
            bool allowActivation,
            out string errorMessage)
        {
            errorMessage = string.Empty;

            if (!_current.HasDefinition || !CanEditCurrentState())
            {
                errorMessage = "Период недоступен для ПДС.";
                return false;
            }

            if (!IsPdsAvailable(_current.Definition))
            {
                errorMessage = "ПДС пока недоступна.";
                return false;
            }

            var currentPds = _current.Definition.PdsAccount;

            if (!allowActivation && currentPds == null)
            {
                errorMessage = "Сначала подключите ПДС.";
                return false;
            }

            if (!NumericInputParser.TryParseNonNegativeAmount(rawContributionAmount, out var contributionAmount)
                || contributionAmount < 0d)
            {
                errorMessage = "Введите корректный взнос в ПДС.";
                return false;
            }

            if (!NumericInputParser.TryParseNonNegativeAmount(rawPensionTransferAmount, out var pensionTransferAmount)
                || pensionTransferAmount < 0d)
            {
                errorMessage = "Введите корректную сумму перевода пенсионных накоплений.";
                return false;
            }

            if (!allowActivation
                && contributionAmount <= 0.01d
                && pensionTransferAmount <= 0.01d)
            {
                errorMessage = "Укажите сумму пополнения или перевода в ПДС.";
                return false;
            }

            var availableContributionAmount = Math.Max(0d, _current.Summary.RemainingToAllocate);

            if (contributionAmount > availableContributionAmount + 0.01d)
            {
                errorMessage = $"Доступно для взноса только {EcuFormatter.FormatAmount(availableContributionAmount)}.";
                return false;
            }

            var pensionReserve = _current.Definition.PensionReserve;
            var availablePensionAmount = pensionReserve != null
                ? Math.Max(0d, pensionReserve.Balance)
                : 0d;

            if (pensionTransferAmount > availablePensionAmount + 0.01d)
            {
                errorMessage = $"Доступно только {EcuFormatter.FormatAmount(availablePensionAmount)} пенсионных накоплений.";
                return false;
            }

            if (currentPds == null && !allowActivation)
            {
                errorMessage = "Сначала подключите ПДС.";
                return false;
            }

            if (currentPds == null)
            {
                currentPds = new PdsAccountRuntime(
                    Guid.NewGuid().ToString("N"),
                    0d,
                    _current.PeriodNumber,
                    0,
                    0d);
            }

            var previousContributionAmount = currentPds.LastContributionPeriodNumber == _current.PeriodNumber
                ? Math.Max(0d, currentPds.LastContributionAmount)
                : 0d;
            var totalContributionAmount = previousContributionAmount + contributionAmount;
            var currentPeriodBonusBefore = ConsumerCreditMath.CalculatePdsContributionBonus(
                previousContributionAmount,
                currentPds.ActivationPeriodNumber,
                _current.PeriodNumber);
            var currentPeriodBonusAfter = ConsumerCreditMath.CalculatePdsContributionBonus(
                totalContributionAmount,
                currentPds.ActivationPeriodNumber,
                _current.PeriodNumber);
            var bonusDelta = Math.Max(0d, currentPeriodBonusAfter - currentPeriodBonusBefore);
            var nextPdsBalance = Math.Max(0d, currentPds.Balance) + contributionAmount + pensionTransferAmount + bonusDelta;
            var nextPds = new PdsAccountRuntime(
                !string.IsNullOrWhiteSpace(currentPds.AccountId)
                    ? currentPds.AccountId
                    : Guid.NewGuid().ToString("N"),
                nextPdsBalance,
                currentPds.ActivationPeriodNumber > 0
                    ? currentPds.ActivationPeriodNumber
                    : _current.PeriodNumber,
                contributionAmount > 0d
                    ? _current.PeriodNumber
                    : currentPds.LastContributionPeriodNumber,
                contributionAmount > 0d
                    ? totalContributionAmount
                    : currentPds.LastContributionAmount);
            var nextPensionReserve = pensionTransferAmount > 0d && pensionReserve != null
                ? new PensionReserveRuntime(
                    Math.Max(0d, pensionReserve.Balance - pensionTransferAmount),
                    pensionReserve.IsAccrualActive,
                    pensionReserve.HasEverBeenActive)
                : pensionReserve;
            var nextOperations = new List<PeriodAssetOperationEntry>(_current.AssetOperations);

            if (contributionAmount > 0d)
            {
                nextOperations.Add(new PeriodAssetOperationEntry(
                    Guid.NewGuid().ToString("N"),
                    ConsumerCreditMath.PdsAssetId,
                    AssetOperationKind.Deposit,
                    FundsSourceType.CurrentIncome,
                    contributionAmount,
                    DateTime.UtcNow.ToString("O")));
            }

            var nextDefinition = BuildStateAwareDefinition(
                _current.Definition,
                pensionReserve: nextPensionReserve,
                replacePensionReserve: true,
                pdsAccount: nextPds,
                replacePdsAccount: true);
            var nextSummary = _calculationEngine.Recalculate(nextDefinition, _current.Expenses, nextOperations);
            var nextState = new PeriodRuntimeState(
                _current.RunId,
                _current.PeriodNumber,
                PeriodFlowState.PeriodActive,
                nextDefinition,
                _current.Expenses,
                nextOperations,
                nextSummary,
                AssetOperationDialogState.Closed,
                _current.IsCheckpointSubmitted,
                BuildPdsStatusMessage(_current.Definition.PdsAccount == null, contributionAmount, pensionTransferAmount),
                string.Empty,
                _current.SubmittedAtUtc,
                true);

            Publish(nextState);
            PersistCurrentState();
            return true;
        }

        public void ApplyPermanentIncomeLoss()
        {
            if (_current == null
                || !_current.HasDefinition
                || (_current.Definition.EconomyContext != null
                    && _current.Definition.EconomyContext.HasPermanentIncomeLoss))
            {
                return;
            }

            var nextState = WithPermanentIncomeLoss(_current);
            Publish(nextState.With(statusMessage: "Располагаемый доход обнулён.", lastError: string.Empty));
            PersistCurrentState();
        }

        public void SubmitPeriod()
        {
            if (!_current.HasDefinition || _current.IsCheckpointSubmitted || !CanEditCurrentState())
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
                        var submittedPeriodNumber = _current.PeriodNumber;
                        var totalPeriods = ResolveTotalPeriodCount(clientRuntime);
                        var hasNextPeriod = totalPeriods > 0 && _current.PeriodNumber < totalPeriods;
                        var nextPeriodNumber = hasNextPeriod
                            ? _current.PeriodNumber + 1
                            : 0;

                        if (hasNextPeriod)
                        {
                            StorePendingCarryOver(_current.RunId, nextPeriodNumber, summary);
                            var syncingState = _current.With(
                                flowState: PeriodFlowState.PeriodCheckpointSubmitting,
                                summary: summary,
                                isCheckpointSubmitted: true,
                                statusMessage: "Синхронизация следующего периода...",
                                lastError: string.Empty,
                                submittedAtUtc: submittedAt,
                                hasPendingLocalChanges: false);

                            Publish(syncingState);
                            PersistState(syncingState);

                            if (_sessionCoordinator != null)
                            {
                                _sessionCoordinator.RefreshBootstrapInPlace((refreshSucceeded, refreshError) =>
                                {
                                    if (!refreshSucceeded)
                                    {
                                        _logger.Warning(
                                            $"Bootstrap refresh after checkpoint failed for run '{clientRuntime.AuthenticatedRun.RunId}', period {submittedPeriodNumber}. {refreshError}");
                                    }

                                    _sessionCoordinator.UpdateLocalRunProgress(nextPeriodNumber, RunLifecycleStatus.InProgress);

                                    var closedState = syncingState.With(
                                        flowState: PeriodFlowState.PeriodClosed,
                                        statusMessage: "Период сохранён.");

                                    PersistState(closedState);
                                    Publish(closedState);

                                    _logger.Info($"Checkpoint submit succeeded for run '{clientRuntime.AuthenticatedRun.RunId}', period {submittedPeriodNumber}.");
                                    _userActionLogger.Log(
                                        UserActionType.Interaction,
                                        "period_checkpoint_submit_succeeded",
                                        BuildPeriodMetadata(closedState));
                                });
                            }
                            else
                            {
                                _logger.Warning(
                                    $"Session coordinator is unavailable after checkpoint for run '{clientRuntime.AuthenticatedRun.RunId}', period {submittedPeriodNumber}. Local progress fallback will be used.");
                                _sessionCoordinator?.UpdateLocalRunProgress(nextPeriodNumber, RunLifecycleStatus.InProgress);

                                var closedState = syncingState.With(
                                    flowState: PeriodFlowState.PeriodClosed,
                                    statusMessage: "Период сохранён.");

                                PersistState(closedState);
                                Publish(closedState);

                                _logger.Info($"Checkpoint submit succeeded for run '{clientRuntime.AuthenticatedRun.RunId}', period {submittedPeriodNumber}.");
                                _userActionLogger.Log(
                                    UserActionType.Interaction,
                                    "period_checkpoint_submit_succeeded",
                                    BuildPeriodMetadata(closedState));
                            }

                            return;
                        }

                        var completedState = _current.With(
                            flowState: PeriodFlowState.PeriodClosed,
                            summary: summary,
                            isCheckpointSubmitted: true,
                            statusMessage: "Период сохранён. Завершение сценария...",
                            lastError: string.Empty,
                            submittedAtUtc: submittedAt,
                            hasPendingLocalChanges: false);
                        PersistState(completedState);
                        Publish(completedState);

                        _logger.Info($"Checkpoint submit succeeded for run '{clientRuntime.AuthenticatedRun.RunId}', period {submittedPeriodNumber}.");
                        _userActionLogger.Log(
                            UserActionType.Interaction,
                            "period_checkpoint_submit_succeeded",
                            BuildPeriodMetadata(completedState));
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
            ClearPendingCarryOver();
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
            PersistState(_current);
        }

        private void PersistState(PeriodRuntimeState state)
        {
            if (_persistenceService == null || state == null || !state.HasDefinition)
            {
                return;
            }

            var carryOverDepositBalance = ResolveCarryOverDepositBalance(state, state.Summary);

            var dto = new PeriodRuntimeSnapshotDto
            {
                runId = state.RunId,
                periodNumber = state.PeriodNumber,
                flowState = PeriodContractMapper.ToPeriodFlowStateCode(state.FlowState),
                hasPersistedInitialBalances = true,
                hasPersistedAccumulatedUje = true,
                initialCashBalance = state.Definition.InitialCashBalance,
                initialDepositBalance = state.Definition.InitialDepositBalance,
                accumulatedUje = state.Definition.CalculationSettings.BaseUje,
                hasPersistedCarryOverBalances = state.Summary != null,
                hasPersistedCarryOverAccumulatedUje = state.Summary != null,
                carryOverTargetPeriodNumber = state.PeriodNumber + 1,
                carryOverCashBalance = state.Summary != null ? state.Summary.CashBalance : 0d,
                carryOverDepositBalance = carryOverDepositBalance,
                carryOverAccumulatedUje = state.Summary != null ? state.Summary.Uje : 0d,
                consumerCredits = BuildConsumerCreditSnapshots(state.Definition.ConsumerCredits),
                residenceOwnership = BuildResidenceOwnershipSnapshot(state.Definition.ResidenceOwnership),
                pensionReserve = BuildPensionReserveSnapshot(state.Definition.PensionReserve),
                pdsAccount = BuildPdsAccountSnapshot(state.Definition.PdsAccount),
                educationGoal = BuildEducationGoalSnapshot(state.Definition.EducationGoal),
                expenses = BuildExpenseSnapshots(state.Expenses),
                assetOperations = BuildOperationSnapshots(state.AssetOperations),
                isCheckpointSubmitted = state.IsCheckpointSubmitted,
                statusMessage = state.StatusMessage,
                lastError = state.LastError,
                submittedAtUtc = state.SubmittedAtUtc,
                hasPendingLocalChanges = state.HasPendingLocalChanges
            };

            _persistenceService.SavePeriodSnapshot(
                state.RunId,
                state.PeriodNumber,
                dto.flowState,
                _serializer.Serialize(dto),
                state.IsCheckpointSubmitted);
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

            if (IsFixedAmountExpense(definition)
                && targetAmount > definition.MinimumAmount + 0.01d)
            {
                validationMessage = $"Для «{definition.Title}» доступен максимум {EcuFormatter.FormatAmount(definition.MinimumAmount)}.";
                return false;
            }

            if (!IsFixedAmountExpense(definition)
                && definition.MaximumAmount > 0.01d
                && targetAmount > definition.MaximumAmount + 0.01d)
            {
                validationMessage = $"Для «{definition.Title}» доступен максимум {EcuFormatter.FormatAmount(definition.MaximumAmount)}.";
                return false;
            }

            if (CanUseDebtForExpense(definition, targetSource))
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

        private static bool IsConsumerCreditAvailable(PeriodRuntimeDefinition definition)
        {
            return definition != null
                   && definition.Meta != null
                   && definition.Meta.HasFeature("consumer_credit");
        }

        private static bool IsMortgageAvailable(PeriodRuntimeDefinition definition)
        {
            return definition != null
                   && definition.Meta != null
                   && definition.Meta.HasFeature("mortgage");
        }

        private static bool IsPdsAvailable(PeriodRuntimeDefinition definition)
        {
            return definition != null
                   && definition.Meta != null
                   && definition.Meta.HasFeature("pds");
        }

        private bool RequiresManualPdsEnrollment()
        {
            var currentRuntime = _sessionCoordinator != null
                ? _sessionCoordinator.CurrentRuntime
                : ClientRuntimeState.Empty;
            var assignedGroupCode = currentRuntime != null
                ? !string.IsNullOrWhiteSpace(currentRuntime.AuthenticatedRun.AssignedGroupCode)
                    ? currentRuntime.AuthenticatedRun.AssignedGroupCode
                    : currentRuntime.Bootstrap != null && currentRuntime.Bootstrap.Participant != null
                        ? currentRuntime.Bootstrap.Participant.AssignedGroupCode
                        : string.Empty
                : string.Empty;

            return string.Equals(
                assignedGroupCode,
                ConsumerCreditMath.ManualPdsEnrollmentGroupCode,
                StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildPdsStatusMessage(
            bool wasActivatedThisPeriod,
            double contributionAmount,
            double pensionTransferAmount)
        {
            if (wasActivatedThisPeriod && contributionAmount > 0.01d && pensionTransferAmount > 0.01d)
            {
                return "ПДС подключена и пополнена.";
            }

            if (wasActivatedThisPeriod && contributionAmount > 0.01d)
            {
                return "ПДС подключена и пополнена.";
            }

            if (wasActivatedThisPeriod && pensionTransferAmount > 0.01d)
            {
                return "ПДС подключена с переводом накоплений.";
            }

            if (wasActivatedThisPeriod)
            {
                return "ПДС подключена.";
            }

            if (contributionAmount > 0.01d && pensionTransferAmount > 0.01d)
            {
                return "ПДС пополнена и дополнена переводом накоплений.";
            }

            if (contributionAmount > 0.01d)
            {
                return "ПДС пополнена.";
            }

            if (pensionTransferAmount > 0.01d)
            {
                return "Пенсионные накопления переведены в ПДС.";
            }

            return "ПДС обновлена.";
        }

        private static bool HasActiveMortgage(IReadOnlyList<ConsumerCreditContractRuntime> credits)
        {
            if (credits == null || credits.Count == 0)
            {
                return false;
            }

            for (var index = 0; index < credits.Count; index++)
            {
                var credit = credits[index];

                if (credit != null
                    && string.Equals(credit.ContractType, ConsumerCreditMath.MortgageKind, StringComparison.Ordinal)
                    && credit.RemainingPeriods > 0
                    && credit.RemainingPrincipal > 0.01d)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsFixedAmountExpense(PeriodExpenseDefinition definition)
        {
            return definition != null
                   && definition.MinimumAmount > 0.01d
                   && definition.MaximumAmount > 0.01d
                   && Math.Abs(definition.MaximumAmount - definition.MinimumAmount) <= 0.01d;
        }

        private static bool TryResolveQuickApplyAmount(PeriodExpenseDefinition definition, out double amount)
        {
            amount = 0d;

            if (definition == null)
            {
                return false;
            }

            if (IsFixedAmountExpense(definition))
            {
                amount = definition.MinimumAmount;
                return true;
            }

            if (string.Equals(definition.Id, "holiday", StringComparison.Ordinal)
                && definition.MaximumAmount > 0.01d)
            {
                amount = definition.MaximumAmount;
                return true;
            }

            return false;
        }

        private bool CanUseDebtForExpense(PeriodExpenseDefinition definition, FundsSourceType targetSource)
        {
            return definition != null
                && definition.IsRequired
                && targetSource == FundsSourceType.Cash
                && _current != null
                && _current.HasDefinition
                && _current.Definition.EconomyContext != null
                && _current.Definition.EconomyContext.HasPermanentIncomeLoss;
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
            var availableText = EcuFormatter.FormatAmount(availableAmount);

            switch (source)
            {
                case FundsSourceType.Cash:
                    return $"Для {title} не хватает наличных. Доступно: {availableText}.";
                case FundsSourceType.Deposit:
                    return $"Для {title} не хватает средств на депозите. Доступно: {availableText}.";
                case FundsSourceType.CurrentIncome:
                default:
                    return $"Для {title} не хватает располагаемого дохода. Осталось распределить: {availableText}.";
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

        private void SellApartment()
        {
            if (!_current.HasDefinition || !CanEditCurrentState() || !HasOwnedResidence(_current.Definition))
            {
                return;
            }

            var summary = _current.Summary ?? PeriodCalculationSummary.Empty;
            var salePrice = ConsumerCreditMath.CalculateResidenceCurrentValue(
                _current.Definition.ResidenceOwnership,
                _current.Definition.EconomyContext);
            var nextDefinition = BuildStateAwareDefinition(
                _current.Definition,
                residenceOwnership: null,
                replaceResidenceOwnership: true,
                initialCashBalance: _current.Definition.InitialCashBalance + Math.Max(0d, salePrice));
            var nextExpenses = BuildResidenceAwareExpenseStates(_current.Expenses, nextDefinition.ExpenseDefinitions);
            var nextSummary = _calculationEngine.Recalculate(nextDefinition, nextExpenses, _current.AssetOperations);
            var nextStatus = summary.CashBalance >= 0d
                ? "Квартира продана."
                : "Квартира продана. Средства зачислены в наличные.";
            var nextState = new PeriodRuntimeState(
                _current.RunId,
                _current.PeriodNumber,
                PeriodFlowState.PeriodActive,
                nextDefinition,
                nextExpenses,
                _current.AssetOperations,
                nextSummary,
                AssetOperationDialogState.Closed,
                _current.IsCheckpointSubmitted,
                nextStatus,
                string.Empty,
                _current.SubmittedAtUtc,
                true);

            Publish(nextState);
            PersistCurrentState();
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
                   && (_current.FlowState == PeriodFlowState.PeriodIntro
                       || _current.FlowState == PeriodFlowState.PeriodActive
                       || _current.FlowState == PeriodFlowState.PeriodValidation);
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

        private void StorePendingCarryOver(string runId, int targetPeriodNumber, PeriodCalculationSummary summary)
        {
            _pendingCarryOverRunId = runId ?? string.Empty;
            _pendingCarryOverTargetPeriodNumber = targetPeriodNumber > 0 ? targetPeriodNumber : 0;
            _pendingCarryOverCashBalance = summary != null ? summary.CashBalance : 0d;
            _pendingCarryOverDepositBalance = ResolveCarryOverDepositBalance(_current, summary);
            _pendingCarryOverAccumulatedUje = summary != null ? summary.Uje : 0d;
        }

        private static double ResolveCarryOverDepositBalance(PeriodRuntimeState state, PeriodCalculationSummary summary)
        {
            var baseDepositBalance = summary != null
                ? Math.Max(0d, summary.DepositBalance)
                : 0d;

            if (state == null || !state.HasDefinition || baseDepositBalance <= 0d)
            {
                return baseDepositBalance;
            }

            var economyContext = state.Definition.EconomyContext ?? PeriodEconomyContext.Empty;
            var normalizedRate = NormalizePercentageToRate(economyContext.DepositRate);

            if (normalizedRate <= 0d)
            {
                return baseDepositBalance;
            }

            return baseDepositBalance * (1d + normalizedRate);
        }

        private static double NormalizePercentageToRate(double? rawPercent)
        {
            if (!rawPercent.HasValue || rawPercent.Value <= 0d)
            {
                return 0d;
            }

            return rawPercent.Value / 100d;
        }

        private void ClearPendingCarryOver()
        {
            _pendingCarryOverRunId = string.Empty;
            _pendingCarryOverTargetPeriodNumber = 0;
            _pendingCarryOverCashBalance = 0d;
            _pendingCarryOverDepositBalance = 0d;
            _pendingCarryOverAccumulatedUje = 0d;
        }

        private PeriodRuntimeState TryApplyCarryOver(PeriodRuntimeState state)
        {
            if (state == null || !state.HasDefinition)
            {
                return state;
            }

            if (IsTrainingResetPeriod(state))
            {
                ClearPendingCarryOver();
                return state;
            }

            if (TryConsumePendingCarryOver(state, out var pendingState))
            {
                return pendingState;
            }

            if (TryLoadCarryOverFromSnapshot(
                    state.RunId,
                    state.PeriodNumber,
                    out var cashBalance,
                    out var depositBalance,
                    out var accumulatedUje))
            {
                return ApplyCarryOver(state, cashBalance, depositBalance, accumulatedUje);
            }

            return state;
        }

        private bool IsTrainingResetPeriod(PeriodRuntimeState state)
        {
            if (state == null
                || !state.HasDefinition
                || state.PeriodNumber <= 1
                || state.Definition.Meta == null
                || !string.Equals(state.Definition.Meta.Phase, "main", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var clientRuntime = _sessionCoordinator != null
                ? _sessionCoordinator.CurrentRuntime
                : ClientRuntimeState.Empty;

            if (clientRuntime == null
                || !clientRuntime.HasSession
                || clientRuntime.Bootstrap == null
                || clientRuntime.Bootstrap.Session == null
                || clientRuntime.Bootstrap.Session.SessionConfig == null
                || clientRuntime.Bootstrap.Session.SessionConfig.Runtime == null)
            {
                return false;
            }

            var sessionConfigRuntime = clientRuntime.Bootstrap.Session.SessionConfig.Runtime;

            if (!sessionConfigRuntime.TryGetPeriod(state.PeriodNumber - 1, out var previousPeriod) || previousPeriod == null)
            {
                return false;
            }

            return string.Equals(previousPeriod.Phase, "training", StringComparison.OrdinalIgnoreCase);
        }

        private bool TryConsumePendingCarryOver(PeriodRuntimeState state, out PeriodRuntimeState nextState)
        {
            nextState = state;

            if (string.IsNullOrWhiteSpace(_pendingCarryOverRunId)
                || _pendingCarryOverTargetPeriodNumber <= 0
                || !string.Equals(state.RunId, _pendingCarryOverRunId, StringComparison.Ordinal)
                || state.PeriodNumber != _pendingCarryOverTargetPeriodNumber)
            {
                return false;
            }

            nextState = ApplyCarryOver(
                state,
                _pendingCarryOverCashBalance,
                _pendingCarryOverDepositBalance,
                _pendingCarryOverAccumulatedUje);
            ClearPendingCarryOver();
            return true;
        }

        private bool TryLoadCarryOverFromSnapshot(
            string runId,
            int targetPeriodNumber,
            out double cashBalance,
            out double depositBalance,
            out double accumulatedUje)
        {
            cashBalance = 0d;
            depositBalance = 0d;
            accumulatedUje = 0d;

            if (_persistenceService == null
                || _serializer == null
                || string.IsNullOrWhiteSpace(runId)
                || targetPeriodNumber <= 1
                || !_persistenceService.TryLoadPeriodSnapshot(out var snapshot)
                || snapshot == null
                || !snapshot.isCheckpointSubmitted
                || !string.Equals(snapshot.runId, runId, StringComparison.Ordinal)
                || snapshot.periodNumber != targetPeriodNumber - 1
                || string.IsNullOrWhiteSpace(snapshot.rawPeriodStateJson))
            {
                return false;
            }

            if (!_serializer.TryDeserialize(
                    snapshot.rawPeriodStateJson,
                    out PeriodRuntimeSnapshotDto dto,
                    out var deserializeError))
            {
                _logger.Warning($"Failed to deserialize persisted carry-over snapshot. {deserializeError}");
                return false;
            }

            if (dto == null
                || dto.carryOverTargetPeriodNumber > 0 && dto.carryOverTargetPeriodNumber != targetPeriodNumber
                || !dto.hasPersistedCarryOverBalances
                || !dto.hasPersistedCarryOverAccumulatedUje)
            {
                return false;
            }

            cashBalance = dto.carryOverCashBalance;
            depositBalance = Math.Max(0d, dto.carryOverDepositBalance);
            accumulatedUje = dto.carryOverAccumulatedUje;
            return true;
        }

        private PeriodRuntimeState ApplyCarryOver(
            PeriodRuntimeState state,
            double cashBalance,
            double depositBalance,
            double accumulatedUje)
        {
            var currentDefinition = state.Definition;
            var nextDefinition = CloneDefinition(
                currentDefinition,
                initialCashBalance: cashBalance,
                initialDepositBalance: Math.Max(0d, depositBalance),
                accumulatedUje: accumulatedUje);
            var nextSummary = _calculationEngine.Recalculate(
                nextDefinition,
                state.Expenses,
                state.AssetOperations);

            return new PeriodRuntimeState(
                state.RunId,
                state.PeriodNumber,
                state.FlowState,
                nextDefinition,
                state.Expenses,
                state.AssetOperations,
                nextSummary,
                state.AssetDialog,
                state.IsCheckpointSubmitted,
                state.StatusMessage,
                state.LastError,
                state.SubmittedAtUtc,
                state.HasPendingLocalChanges);
        }

        private PeriodRuntimeState WithPermanentIncomeLoss(PeriodRuntimeState state)
        {
            if (state == null || !state.HasDefinition)
            {
                return state;
            }

            var currentDefinition = state.Definition;
            var currentEconomy = currentDefinition.EconomyContext ?? PeriodEconomyContext.Empty;
            var nextEconomy = new PeriodEconomyContext(
                currentEconomy.CurrentPeriodNumber,
                currentEconomy.HistoricalYear,
                currentEconomy.NominalIncomeGrowth,
                currentEconomy.Inflation,
                currentEconomy.DepositRate,
                currentEconomy.CreditRate,
                currentEconomy.MortgageRate,
                currentEconomy.BaseIncomeEcu,
                0d,
                currentEconomy.ReferenceIncomeEcu,
                currentEconomy.ExpenseInflationMultiplier,
                currentEconomy.CurrentInflationMultiplier,
                currentEconomy.CashValueMultiplier,
                currentEconomy.DepositValueMultiplier,
                true);
            var nextDefinition = CloneDefinition(
                currentDefinition,
                economyContext: nextEconomy,
                currentIncomeEcu: 0d);
            var nextExpenses = BuildPostFiringExpenseStates(state.Expenses, currentDefinition.ExpenseDefinitions);
            var nextSummary = _calculationEngine.Recalculate(
                nextDefinition,
                nextExpenses,
                state.AssetOperations);

            return new PeriodRuntimeState(
                state.RunId,
                state.PeriodNumber,
                state.FlowState,
                nextDefinition,
                nextExpenses,
                state.AssetOperations,
                nextSummary,
                state.AssetDialog,
                state.IsCheckpointSubmitted,
                state.StatusMessage,
                state.LastError,
                state.SubmittedAtUtc,
                state.HasPendingLocalChanges);
        }

        private static PeriodRuntimeDefinition CloneDefinition(
            PeriodRuntimeDefinition definition,
            IReadOnlyList<PeriodInfoBlockValue> infoBlockValues = null,
            IReadOnlyList<ConsumerCreditContractRuntime> consumerCredits = null,
            ResidenceOwnershipRuntime residenceOwnership = null,
            bool replaceResidenceOwnership = false,
            PensionReserveRuntime pensionReserve = null,
            bool replacePensionReserve = false,
            PdsAccountRuntime pdsAccount = null,
            bool replacePdsAccount = false,
            EducationGoalRuntime educationGoal = null,
            bool replaceEducationGoal = false,
            IReadOnlyList<PeriodExpenseDefinition> expenseDefinitions = null,
            IReadOnlyList<PeriodAssetDefinition> assetDefinitions = null,
            double? initialCashBalance = null,
            double? initialDepositBalance = null,
            double? accumulatedUje = null,
            PeriodEconomyContext economyContext = null,
            double? currentIncomeEcu = null)
        {
            if (definition == null)
            {
                return null;
            }

            var settings = definition.CalculationSettings ?? new PeriodCalculationSettings(0d, 0d, 0d, 0d);

            return new PeriodRuntimeDefinition(
                definition.Meta,
                infoBlockValues ?? definition.InfoBlockValues,
                expenseDefinitions ?? definition.ExpenseDefinitions,
                assetDefinitions ?? definition.AssetDefinitions,
                definition.ValidationSettings,
                new PeriodCalculationSettings(
                    currentIncomeEcu ?? settings.CurrentIncomeEcu,
                    settings.BaseIncomeEcu,
                    accumulatedUje ?? settings.BaseUje,
                    settings.MaximumUje),
                economyContext ?? definition.EconomyContext,
                consumerCredits ?? definition.ConsumerCredits,
                replaceResidenceOwnership
                    ? residenceOwnership
                    : residenceOwnership ?? definition.ResidenceOwnership,
                replacePensionReserve
                    ? pensionReserve
                    : pensionReserve ?? definition.PensionReserve,
                replacePdsAccount
                    ? pdsAccount
                    : pdsAccount ?? definition.PdsAccount,
                replaceEducationGoal
                    ? educationGoal
                    : educationGoal ?? definition.EducationGoal,
                initialCashBalance ?? definition.InitialCashBalance,
                initialDepositBalance ?? definition.InitialDepositBalance,
                definition.SourceSummary);
        }

        private static PeriodRuntimeDefinition BuildStateAwareDefinition(
            PeriodRuntimeDefinition definition,
            IReadOnlyList<ConsumerCreditContractRuntime> consumerCredits = null,
            ResidenceOwnershipRuntime residenceOwnership = null,
            bool replaceResidenceOwnership = false,
            PensionReserveRuntime pensionReserve = null,
            bool replacePensionReserve = false,
            PdsAccountRuntime pdsAccount = null,
            bool replacePdsAccount = false,
            EducationGoalRuntime educationGoal = null,
            bool replaceEducationGoal = false,
            double? initialCashBalance = null,
            double? initialDepositBalance = null,
            double? accumulatedUje = null,
            PeriodEconomyContext economyContext = null,
            double? currentIncomeEcu = null)
        {
            if (definition == null)
            {
                return null;
            }

            var targetResidence = replaceResidenceOwnership
                ? residenceOwnership
                : residenceOwnership ?? definition.ResidenceOwnership;
            var targetPensionReserve = replacePensionReserve
                ? pensionReserve
                : pensionReserve ?? definition.PensionReserve;
            var targetPdsAccount = replacePdsAccount
                ? pdsAccount
                : pdsAccount ?? definition.PdsAccount;
            var targetEducationGoal = replaceEducationGoal
                ? educationGoal
                : educationGoal ?? definition.EducationGoal;
            var targetEconomyContext = economyContext ?? definition.EconomyContext;
            return CloneDefinition(
                definition,
                infoBlockValues: BuildInfoBlockValues(
                    definition.InfoBlockValues,
                    targetPensionReserve),
                consumerCredits: consumerCredits,
                residenceOwnership: targetResidence,
                replaceResidenceOwnership: true,
                pensionReserve: targetPensionReserve,
                replacePensionReserve: true,
                pdsAccount: targetPdsAccount,
                replacePdsAccount: true,
                educationGoal: targetEducationGoal,
                replaceEducationGoal: true,
                expenseDefinitions: BuildResidenceAwareExpenseDefinitions(
                    definition.ExpenseDefinitions,
                    targetEconomyContext,
                    targetResidence != null),
                assetDefinitions: BuildStateAwareAssetDefinitions(
                    definition.AssetDefinitions,
                    targetResidence != null,
                    targetPdsAccount != null),
                initialCashBalance: initialCashBalance,
                initialDepositBalance: initialDepositBalance,
                accumulatedUje: accumulatedUje,
                economyContext: targetEconomyContext,
                currentIncomeEcu: currentIncomeEcu);
        }

        private static IReadOnlyList<PeriodInfoBlockValue> BuildInfoBlockValues(
            IReadOnlyList<PeriodInfoBlockValue> currentValues,
            PensionReserveRuntime pensionReserve)
        {
            var result = new List<PeriodInfoBlockValue>();

            if (currentValues != null)
            {
                for (var index = 0; index < currentValues.Count; index++)
                {
                    var currentValue = currentValues[index];

                    if (currentValue == null
                        || string.Equals(currentValue.Id, "pension_savings", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    result.Add(currentValue);
                }
            }

            var pensionInfoValue = BuildPensionInfoValue(pensionReserve);

            if (pensionInfoValue != null)
            {
                result.Add(pensionInfoValue);
            }

            return result;
        }

        private static PeriodInfoBlockValue BuildPensionInfoValue(PensionReserveRuntime pensionReserve)
        {
            if (pensionReserve == null || !pensionReserve.HasEverBeenActive)
            {
                return null;
            }

            return pensionReserve.IsAccrualActive
                ? new PeriodInfoBlockValue(
                    "pension_savings",
                    "Накопления на пенсию",
                    Math.Max(0d, pensionReserve.Balance),
                    " ECU",
                    string.Empty)
                : new PeriodInfoBlockValue(
                    "pension_savings",
                    "Накопления на пенсию",
                    null,
                    string.Empty,
                    $"{Math.Max(0d, pensionReserve.Balance).ToString("0.##", CultureInfo.InvariantCulture)} ECU (начисления заморожены)");
        }

        private static IReadOnlyList<PeriodExpenseDefinition> BuildResidenceAwareExpenseDefinitions(
            IReadOnlyList<PeriodExpenseDefinition> definitions,
            PeriodEconomyContext economyContext,
            bool hasOwnedResidence)
        {
            var result = new List<PeriodExpenseDefinition>();
            var hasHousingRent = false;

            if (definitions != null)
            {
                for (var index = 0; index < definitions.Count; index++)
                {
                    var definition = definitions[index];

                    if (definition == null)
                    {
                        continue;
                    }

                    if (string.Equals(definition.Id, "housing_rent", StringComparison.Ordinal))
                    {
                        hasHousingRent = true;

                        if (hasOwnedResidence)
                        {
                            continue;
                        }
                    }

                    result.Add(definition);
                }
            }

            if (!hasOwnedResidence && !hasHousingRent)
            {
                result.Insert(Math.Min(1, result.Count), BuildHousingRentExpenseDefinition(economyContext));
            }

            return result;
        }

        private static IReadOnlyList<PeriodExpenseState> BuildResidenceAwareExpenseStates(
            IReadOnlyList<PeriodExpenseState> expenses,
            IReadOnlyList<PeriodExpenseDefinition> definitions)
        {
            var result = new List<PeriodExpenseState>();
            var hasHousingRent = false;
            var rentDefinition = FindDefinition(definitions, "housing_rent");

            if (expenses != null)
            {
                for (var index = 0; index < expenses.Count; index++)
                {
                    var expense = expenses[index];

                    if (expense == null)
                    {
                        continue;
                    }

                    if (string.Equals(expense.ExpenseId, "housing_rent", StringComparison.Ordinal))
                    {
                        if (rentDefinition == null)
                        {
                            continue;
                        }

                        hasHousingRent = true;
                    }

                    result.Add(expense);
                }
            }

            if (rentDefinition != null && !hasHousingRent)
            {
                var source = rentDefinition.AllowedSources != null && rentDefinition.AllowedSources.Count > 0
                    ? rentDefinition.AllowedSources[0]
                    : FundsSourceType.CurrentIncome;
                result.Add(new PeriodExpenseState("housing_rent", 0d, source));
            }

            return result;
        }

        private static IReadOnlyList<PeriodAssetDefinition> BuildStateAwareAssetDefinitions(
            IReadOnlyList<PeriodAssetDefinition> definitions,
            bool hasOwnedResidence,
            bool hasPdsAccount)
        {
            var result = new List<PeriodAssetDefinition>();
            var hasApartment = false;
            var hasPds = false;

            if (definitions != null)
            {
                for (var index = 0; index < definitions.Count; index++)
                {
                    var definition = definitions[index];

                    if (definition == null)
                    {
                        continue;
                    }

                    if (definition.AssetType == PeriodAssetType.Apartment
                        || string.Equals(definition.Id, ConsumerCreditMath.ApartmentResidenceId, StringComparison.Ordinal))
                    {
                        hasApartment = true;

                        if (!hasOwnedResidence)
                        {
                            continue;
                        }
                    }

                    if (definition.AssetType == PeriodAssetType.Pds
                        || string.Equals(definition.Id, ConsumerCreditMath.PdsAssetId, StringComparison.Ordinal))
                    {
                        hasPds = true;

                        if (!hasPdsAccount)
                        {
                            continue;
                        }
                    }

                    result.Add(definition);
                }
            }

            if (hasOwnedResidence && !hasApartment)
            {
                result.Add(new PeriodAssetDefinition(
                    ConsumerCreditMath.ApartmentResidenceId,
                    "Квартира",
                    PeriodAssetType.Apartment,
                    false,
                    true,
                    Array.Empty<FundsSourceType>(),
                    "Собственное жильё участника."));
            }

            if (hasPdsAccount && !hasPds)
            {
                result.Add(new PeriodAssetDefinition(
                    ConsumerCreditMath.PdsAssetId,
                    "ПДС",
                    PeriodAssetType.Pds,
                    false,
                    false,
                    Array.Empty<FundsSourceType>(),
                    "Программа долгосрочных сбережений."));
            }

            return result;
        }

        private static PeriodExpenseDefinition BuildHousingRentExpenseDefinition(PeriodEconomyContext economyContext)
        {
            var amount = Math.Max(0d, ConsumerCreditMath.CalculateApartmentCost(economyContext) * 0.10d);

            return new PeriodExpenseDefinition(
                "housing_rent",
                "Аренда жилья",
                true,
                0d,
                amount,
                amount,
                new[] { FundsSourceType.CurrentIncome, FundsSourceType.Cash },
                0d,
                amount,
                "Обязательная статья периода.");
        }

        private static ResidenceOwnershipRuntime BuildResidenceOwnership(
            int periodNumber,
            PeriodEconomyContext economyContext,
            string acquisitionMode)
        {
            return new ResidenceOwnershipRuntime(
                ConsumerCreditMath.ApartmentResidenceId,
                ConsumerCreditMath.CalculateApartmentCost(economyContext),
                periodNumber,
                economyContext != null
                    ? Math.Max(0.0001d, economyContext.ExpenseInflationMultiplier)
                    : 1d,
                acquisitionMode);
        }

        private static bool HasOwnedResidence(PeriodRuntimeDefinition definition)
        {
            return definition != null && definition.ResidenceOwnership != null;
        }

        private static PeriodExpenseDefinition FindDefinition(
            IReadOnlyList<PeriodExpenseDefinition> definitions,
            string expenseId)
        {
            if (definitions == null || string.IsNullOrWhiteSpace(expenseId))
            {
                return null;
            }

            for (var index = 0; index < definitions.Count; index++)
            {
                var definition = definitions[index];

                if (definition != null && string.Equals(definition.Id, expenseId, StringComparison.Ordinal))
                {
                    return definition;
                }
            }

            return null;
        }

        private static IReadOnlyList<PeriodExpenseState> BuildPostFiringExpenseStates(
            IReadOnlyList<PeriodExpenseState> expenses,
            IReadOnlyList<PeriodExpenseDefinition> definitions)
        {
            if (expenses == null || expenses.Count == 0)
            {
                return Array.Empty<PeriodExpenseState>();
            }

            var result = new List<PeriodExpenseState>(expenses.Count);

            for (var index = 0; index < expenses.Count; index++)
            {
                var expenseState = expenses[index];

                if (expenseState == null)
                {
                    continue;
                }

                var nextSource = expenseState.Source;

                if (nextSource == FundsSourceType.CurrentIncome
                    && DefinitionAllowsCash(definitions, expenseState.ExpenseId))
                {
                    nextSource = FundsSourceType.Cash;
                }

                result.Add(nextSource == expenseState.Source
                    ? expenseState
                    : expenseState.With(source: nextSource));
            }

            return result;
        }

        private static ConsumerCreditContractSnapshotDto[] BuildConsumerCreditSnapshots(
            IReadOnlyList<ConsumerCreditContractRuntime> credits)
        {
            if (credits == null || credits.Count == 0)
            {
                return Array.Empty<ConsumerCreditContractSnapshotDto>();
            }

            var result = new ConsumerCreditContractSnapshotDto[credits.Count];

            for (var index = 0; index < credits.Count; index++)
            {
                var credit = credits[index];
                result[index] = credit == null
                    ? null
                    : new ConsumerCreditContractSnapshotDto
                    {
                        contractType = credit.ContractType,
                        creditId = credit.CreditId,
                        originationPeriodNumber = credit.OriginationPeriodNumber,
                        originalPrincipal = credit.OriginalPrincipal,
                        remainingPrincipal = credit.RemainingPrincipal,
                        periodicPayment = credit.PeriodicPayment,
                        fixedRatePercent = credit.FixedRatePercent,
                        remainingPeriods = credit.RemainingPeriods
                    };
            }

            return result;
        }

        private static ResidenceOwnershipSnapshotDto BuildResidenceOwnershipSnapshot(
            ResidenceOwnershipRuntime residenceOwnership)
        {
            if (residenceOwnership == null)
            {
                return null;
            }

            return new ResidenceOwnershipSnapshotDto
            {
                residenceId = residenceOwnership.ResidenceId,
                purchasePrice = residenceOwnership.PurchasePrice,
                purchasePeriodNumber = residenceOwnership.PurchasePeriodNumber,
                purchaseInflationMultiplier = residenceOwnership.PurchaseInflationMultiplier,
                acquisitionMode = residenceOwnership.AcquisitionMode
            };
        }

        private static PensionReserveSnapshotDto BuildPensionReserveSnapshot(PensionReserveRuntime pensionReserve)
        {
            if (pensionReserve == null)
            {
                return null;
            }

            return new PensionReserveSnapshotDto
            {
                balance = pensionReserve.Balance,
                isAccrualActive = pensionReserve.IsAccrualActive,
                hasEverBeenActive = pensionReserve.HasEverBeenActive
            };
        }

        private static PdsAccountSnapshotDto BuildPdsAccountSnapshot(PdsAccountRuntime pdsAccount)
        {
            if (pdsAccount == null)
            {
                return null;
            }

            return new PdsAccountSnapshotDto
            {
                accountId = pdsAccount.AccountId,
                balance = pdsAccount.Balance,
                activationPeriodNumber = pdsAccount.ActivationPeriodNumber,
                lastContributionPeriodNumber = pdsAccount.LastContributionPeriodNumber,
                lastContributionAmount = pdsAccount.LastContributionAmount
            };
        }

        private static EducationGoalSnapshotDto BuildEducationGoalSnapshot(EducationGoalRuntime educationGoal)
        {
            if (educationGoal == null)
            {
                return null;
            }

            return new EducationGoalSnapshotDto
            {
                accumulatedAmount = educationGoal.AccumulatedAmount,
                targetAmount = educationGoal.TargetAmount,
                goalReachedPeriodNumber = educationGoal.GoalReachedPeriodNumber,
                incomeBoostStartPeriodNumber = educationGoal.IncomeBoostStartPeriodNumber
            };
        }

        private static bool DefinitionAllowsCash(
            IReadOnlyList<PeriodExpenseDefinition> definitions,
            string expenseId)
        {
            if (definitions == null || string.IsNullOrWhiteSpace(expenseId))
            {
                return false;
            }

            for (var index = 0; index < definitions.Count; index++)
            {
                var definition = definitions[index];

                if (definition == null || !string.Equals(definition.Id, expenseId, StringComparison.Ordinal))
                {
                    continue;
                }

                var allowedSources = definition.AllowedSources;

                if (allowedSources == null)
                {
                    return false;
                }

                for (var sourceIndex = 0; sourceIndex < allowedSources.Count; sourceIndex++)
                {
                    if (allowedSources[sourceIndex] == FundsSourceType.Cash)
                    {
                        return true;
                    }
                }

                return false;
            }

            return false;
        }
    }
}
