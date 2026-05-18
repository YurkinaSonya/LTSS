using System;
using System.Collections.Generic;
using Game.Core.Application.Logging;
using Game.Core.Application.Navigation;
using Game.Core.Application.State;
using Game.Core.Events;
using Game.Domain.GameFlow;

namespace Game.Core.Application.Session
{
    public sealed class SessionCoordinator : ISessionCoordinator
    {
        private readonly IAuthApiClient _authApiClient;
        private readonly IRunApiClient _runApiClient;
        private readonly ISessionRuntimeFactory _runtimeFactory;
        private readonly ISessionPersistenceService _persistenceService;
        private readonly IApplicationNavigationService _navigation;
        private readonly IApplicationStateStore _stateStore;
        private readonly IGameSessionService _gameSessionService;
        private readonly IUserActionLogger _userActionLogger;
        private readonly IAppLogger _logger;
        private readonly IEventAggregator _eventAggregator;

        private ClientRuntimeState _currentRuntime = ClientRuntimeState.Empty;
        private AuthTokenData _currentTokenData = AuthTokenData.Empty;
        private AuthenticatedRunInfo _currentRunInfo = AuthenticatedRunInfo.Empty;
        private bool _isBusy;

        public ClientRuntimeState CurrentRuntime => _currentRuntime;

        public event Action<ClientRuntimeState> RuntimeChanged;

        public SessionCoordinator(
            IAuthApiClient authApiClient,
            IRunApiClient runApiClient,
            ISessionRuntimeFactory runtimeFactory,
            ISessionPersistenceService persistenceService,
            IApplicationNavigationService navigation,
            IApplicationStateStore stateStore,
            IGameSessionService gameSessionService,
            IUserActionLogger userActionLogger,
            IAppLogger logger,
            IEventAggregator eventAggregator)
        {
            _authApiClient = authApiClient;
            _runApiClient = runApiClient;
            _runtimeFactory = runtimeFactory;
            _persistenceService = persistenceService;
            _navigation = navigation;
            _stateStore = stateStore;
            _gameSessionService = gameSessionService;
            _userActionLogger = userActionLogger;
            _logger = logger;
            _eventAggregator = eventAggregator;
        }

        public void RestoreIfPossible(AppStateId fallbackState = AppStateId.Login)
        {
            if (_isBusy)
            {
                return;
            }

            _logger.Info("Session restore started.");
            _userActionLogger.Log(
                UserActionType.SessionLifecycle,
                "session_restore_started");

            if (!_persistenceService.TryLoadAuthContext(out var persistedAuthContext)
                || persistedAuthContext == null)
            {
                PublishRuntime(ClientRuntimeState.Empty);
                _navigation.MoveTo(fallbackState, "session_restore_not_available");
                _logger.Info("No persisted auth context was found.");
                return;
            }

            _currentTokenData = persistedAuthContext.ToTokenData();
            _currentRunInfo = persistedAuthContext.ToRunInfo();

            if (!_currentTokenData.IsValid || !_currentRunInfo.IsValid)
            {
                PublishRuntime(ClientRuntimeState.Empty);
                _navigation.MoveTo(fallbackState, "session_restore_invalid_context");
                _logger.Warning("Persisted auth context is incomplete.");
                return;
            }

            _isBusy = true;
            _navigation.MoveTo(AppStateId.LoadingSession, "session_restore_remote_check");

            _runApiClient.GetCurrent(_currentTokenData.Token, response =>
            {
                _isBusy = false;

                if (response.IsSuccess && response.Payload != null)
                {
                    _currentRunInfo = SessionContractMapper.ToAuthenticatedRunInfo(response.Payload);

                    if (!_currentRunInfo.IsValid)
                    {
                        _currentRunInfo = persistedAuthContext.ToRunInfo();
                    }

                    _persistenceService.SaveAuthContext(_currentTokenData, _currentRunInfo);

                    _logger.Info($"Session restore run resolved for '{_currentRunInfo.RunId}'.");
                    _userActionLogger.Log(
                        UserActionType.SessionLifecycle,
                        "session_restore_remote_success",
                        BuildSessionMetadata(_currentRunInfo));

                    LoadBootstrapInternal(allowSnapshotFallback: true, loginFallbackState: fallbackState, reason: "restore");
                    return;
                }

                if (TryRestoreFromSnapshot(out var runtimeState, out _))
                {
                    PublishRuntime(runtimeState);
                    _navigation.ShowSessionReady("session_restore_snapshot");

                    _logger.Info("Session restore fell back to stored bootstrap snapshot.");
                    _userActionLogger.Log(
                        UserActionType.SessionLifecycle,
                        "session_restore_snapshot_success",
                        BuildSessionMetadata(_currentRunInfo));
                    return;
                }

                var errorMessage = BuildApiError("Не удалось восстановить сессию.", response);
                _logger.Warning(errorMessage);
                SetRecoverableError(errorMessage, fallbackState, "session_restore_failed");
            });
        }

        public void Login(string login, string password)
        {
            if (_isBusy)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
            {
                _stateStore.SetState(state => state.With(
                    lastError: "Введите логин и пароль."));
                return;
            }

            _isBusy = true;
            _navigation.MoveTo(AppStateId.Authenticating, "login_request");

            _logger.Info("Login request started.");
            _userActionLogger.Log(
                UserActionType.SessionLifecycle,
                "login_started");

            _authApiClient.Login(new LoginRequestDto(login, password), response =>
            {
                _isBusy = false;

                if (!response.IsSuccess || response.Payload == null)
                {
                    var errorMessage = BuildApiError("Не удалось выполнить вход.", response);
                    _logger.Warning(errorMessage);
                    SetRecoverableError(errorMessage, AppStateId.Login, "login_failed");
                    return;
                }

                if (string.IsNullOrWhiteSpace(response.Payload.token))
                {
                    HandleFatalError("Вход выполнен, но токен не получен.", "login_invalid_response");
                    return;
                }

                _currentTokenData = new AuthTokenData(
                    response.Payload.token,
                    DateTime.UtcNow.ToString("O"));
                _currentRunInfo = SessionContractMapper.ToAuthenticatedRunInfo(response.Payload.run);

                if (!_currentRunInfo.IsValid)
                {
                    HandleFatalError("Вход выполнен, но данные запуска отсутствуют.", "login_missing_run");
                    return;
                }

                _persistenceService.SaveAuthContext(_currentTokenData, _currentRunInfo);

                _logger.Info($"Login succeeded for run '{_currentRunInfo.RunId}'.");
                _userActionLogger.Log(
                    UserActionType.SessionLifecycle,
                    "login_succeeded",
                    BuildSessionMetadata(_currentRunInfo));

                LoadBootstrapInternal(allowSnapshotFallback: false, loginFallbackState: AppStateId.Login, reason: "login");
            });
        }

        public void LoadBootstrap()
        {
            if (_isBusy)
            {
                return;
            }

            if (!_currentTokenData.IsValid || !_currentRunInfo.IsValid)
            {
                _navigation.GoToLogin("bootstrap_requires_auth");
                return;
            }

            LoadBootstrapInternal(allowSnapshotFallback: true, loginFallbackState: AppStateId.Login, reason: "manual");
        }

        public void RefreshBootstrapInPlace(Action<bool, string> onCompleted = null)
        {
            if (_isBusy)
            {
                onCompleted?.Invoke(false, "Сессия уже выполняет другую операцию.");
                return;
            }

            if (!_currentTokenData.IsValid || !_currentRunInfo.IsValid)
            {
                onCompleted?.Invoke(false, "Сессия недоступна для синхронизации.");
                return;
            }

            _isBusy = true;
            _logger.Info($"Bootstrap in-place refresh started for run '{_currentRunInfo.RunId}'.");

            _runApiClient.GetBootstrap(_currentRunInfo.RunId, _currentTokenData.Token, response =>
            {
                _isBusy = false;

                if (response.IsSuccess && response.Payload != null)
                {
                    if (!_runtimeFactory.TryBuild(
                            _currentTokenData,
                            _currentRunInfo,
                            response.Payload,
                            out var runtimeState,
                            out var buildError))
                    {
                        _logger.Warning(buildError);
                        onCompleted?.Invoke(false, buildError);
                        return;
                    }

                    runtimeState = MergeBootstrapWithCurrentRuntime(runtimeState);
                    _persistenceService.SaveAuthContext(runtimeState.AuthToken, runtimeState.AuthenticatedRun);
                    _persistenceService.SaveBootstrapSnapshot(
                        runtimeState.AuthenticatedRun,
                        runtimeState.Bootstrap.BootstrapVersion,
                        response.RawResponse);

                    PublishRuntime(runtimeState);
                    _logger.Info($"Bootstrap in-place refresh succeeded for run '{runtimeState.AuthenticatedRun.RunId}'.");
                    onCompleted?.Invoke(true, string.Empty);
                    return;
                }

                var errorMessage = BuildApiError("Не удалось синхронизировать данные сессии.", response);
                _logger.Warning(errorMessage);
                onCompleted?.Invoke(false, errorMessage);
            });
        }

        private ClientRuntimeState MergeBootstrapWithCurrentRuntime(ClientRuntimeState refreshedRuntime)
        {
            if (refreshedRuntime == null
                || !refreshedRuntime.HasSession
                || _currentRuntime == null
                || !_currentRuntime.HasSession
                || _currentRuntime.Bootstrap == null)
            {
                return refreshedRuntime ?? ClientRuntimeState.Empty;
            }

            var refreshedBootstrap = refreshedRuntime.Bootstrap;
            var currentBootstrap = _currentRuntime.Bootstrap;
            var mergedSession = ShouldKeepExistingSession(refreshedBootstrap.Session, currentBootstrap.Session)
                ? currentBootstrap.Session
                : refreshedBootstrap.Session;
            var mergedParticipant = ShouldKeepExistingParticipant(refreshedBootstrap.Participant, currentBootstrap.Participant)
                ? currentBootstrap.Participant
                : refreshedBootstrap.Participant;
            var mergedSurveyTemplates = refreshedBootstrap.SurveyTemplates == null || refreshedBootstrap.SurveyTemplates.Count == 0
                ? currentBootstrap.SurveyTemplates
                : refreshedBootstrap.SurveyTemplates;
            var mergedDataset = refreshedBootstrap.StatisticalDataset == null || !refreshedBootstrap.StatisticalDataset.HasDataset
                ? currentBootstrap.StatisticalDataset
                : refreshedBootstrap.StatisticalDataset;
            var mergedBootstrap = new BootstrapPayload(
                refreshedBootstrap.Run,
                mergedSession,
                mergedParticipant,
                mergedSurveyTemplates,
                mergedDataset);

            return new ClientRuntimeState(
                refreshedRuntime.AuthToken,
                refreshedRuntime.AuthenticatedRun,
                mergedBootstrap,
                refreshedRuntime.CanRestore);
        }

        private static bool ShouldKeepExistingSession(SessionRuntimeModel refreshed, SessionRuntimeModel existing)
        {
            if (existing == null)
            {
                return false;
            }

            if (refreshed == null)
            {
                return true;
            }

            return (refreshed.SessionDefinitionId <= 0 && string.IsNullOrWhiteSpace(refreshed.Code))
                   || refreshed.SessionConfig == null
                   || (!refreshed.SessionConfig.Document.IsValid && existing.SessionConfig != null && existing.SessionConfig.Document.IsValid)
                   || (refreshed.SessionConfig.Document.IsEmpty && existing.SessionConfig != null && !existing.SessionConfig.Document.IsEmpty)
                   || (refreshed.SessionConfig.Runtime == null || !refreshed.SessionConfig.Runtime.IsValid)
                      && existing.SessionConfig != null
                      && existing.SessionConfig.Runtime != null
                      && existing.SessionConfig.Runtime.IsValid;
        }

        private static bool ShouldKeepExistingParticipant(ParticipantRuntimeModel refreshed, ParticipantRuntimeModel existing)
        {
            if (existing == null)
            {
                return false;
            }

            if (refreshed == null)
            {
                return true;
            }

            return (refreshed.ParticipantAccountId <= 0 && string.IsNullOrWhiteSpace(refreshed.Login))
                   || refreshed.AssignedConfig == null
                   || (!refreshed.AssignedConfig.Document.IsValid && existing.AssignedConfig != null && existing.AssignedConfig.Document.IsValid)
                   || (refreshed.AssignedConfig.Document.IsEmpty && existing.AssignedConfig != null && !existing.AssignedConfig.Document.IsEmpty);
        }

        public void UpdateLocalRunProgress(int currentPeriodNumber, RunLifecycleStatus runStatus)
        {
            if (!_currentRuntime.HasSession || currentPeriodNumber <= 0)
            {
                return;
            }

            var nextRunStatus = runStatus != RunLifecycleStatus.Unknown
                ? runStatus
                : _currentRunInfo.RunStatus;
            var nextRunInfo = new AuthenticatedRunInfo(
                _currentRunInfo.RunId,
                _currentRunInfo.SessionDefinitionCode,
                nextRunStatus,
                currentPeriodNumber,
                _currentRunInfo.AssignedGroupCode);
            var bootstrapRun = _currentRuntime.Bootstrap != null
                ? _currentRuntime.Bootstrap.Run
                : null;
            var nextBootstrapRun = new BootstrapRunRuntimeModel(
                bootstrapRun != null ? bootstrapRun.RunId : nextRunInfo.RunId,
                nextRunStatus,
                currentPeriodNumber,
                bootstrapRun != null ? bootstrapRun.BootstrapVersion : 0,
                bootstrapRun != null ? bootstrapRun.StartedAtRaw : string.Empty,
                bootstrapRun != null ? bootstrapRun.FinishedAtRaw : string.Empty,
                bootstrapRun != null ? bootstrapRun.LastCheckpointAtRaw : string.Empty);
            var nextBootstrap = new BootstrapPayload(
                nextBootstrapRun,
                _currentRuntime.Bootstrap != null ? _currentRuntime.Bootstrap.Session : null,
                _currentRuntime.Bootstrap != null ? _currentRuntime.Bootstrap.Participant : null,
                _currentRuntime.Bootstrap != null ? _currentRuntime.Bootstrap.SurveyTemplates : null,
                _currentRuntime.Bootstrap != null ? _currentRuntime.Bootstrap.StatisticalDataset : null);
            var nextRuntime = new ClientRuntimeState(
                _currentRuntime.AuthToken,
                nextRunInfo,
                nextBootstrap,
                _currentRuntime.CanRestore);

            _persistenceService.SaveAuthContext(nextRuntime.AuthToken, nextRunInfo);
            PublishRuntime(nextRuntime);
        }

        public void CompleteRunLocally(string statusMessage = null, string reason = null)
        {
            if (!_currentRuntime.HasSession)
            {
                return;
            }

            var completedPeriodNumber = _currentRunInfo.CurrentPeriodNumber > 0
                ? _currentRunInfo.CurrentPeriodNumber
                : _currentRuntime.Bootstrap.Run.CurrentPeriodNumber;
            UpdateLocalRunProgress(completedPeriodNumber, RunLifecycleStatus.Completed);
            _navigation.ShowSessionReady(reason ?? "run_completed");
            _stateStore.SetState(state => state.With(
                statusMessage: string.IsNullOrWhiteSpace(statusMessage)
                    ? "Все периоды завершены. Далее будет пост-экспериментальный этап."
                    : statusMessage,
                lastError: string.Empty));
        }

        public void ClearSession(string reason = null)
        {
            _isBusy = false;
            _currentTokenData = AuthTokenData.Empty;
            _currentRunInfo = AuthenticatedRunInfo.Empty;

            _persistenceService.ClearAll();
            PublishRuntime(ClientRuntimeState.Empty);

            _stateStore.SetState(state => state.With(
                statusMessage: string.Empty,
                lastError: string.Empty));

            _userActionLogger.Log(
                UserActionType.SessionLifecycle,
                "session_cleared");

            _logger.Info("Stored session has been cleared.");
            _navigation.GoToLogin(reason ?? "session_cleared");
        }

        private void LoadBootstrapInternal(
            bool allowSnapshotFallback,
            AppStateId loginFallbackState,
            string reason)
        {
            if (!_currentTokenData.IsValid || !_currentRunInfo.IsValid)
            {
                _navigation.MoveTo(loginFallbackState, "bootstrap_missing_auth");
                return;
            }

            _isBusy = true;
            _navigation.MoveTo(AppStateId.LoadingSession, $"bootstrap_{reason}");

            _logger.Info($"Bootstrap load started for run '{_currentRunInfo.RunId}'.");
            _userActionLogger.Log(
                UserActionType.SessionLifecycle,
                "bootstrap_load_started",
                BuildSessionMetadata(_currentRunInfo));

            _runApiClient.GetBootstrap(_currentRunInfo.RunId, _currentTokenData.Token, response =>
            {
                _isBusy = false;

                if (response.IsSuccess && response.Payload != null)
                {
                    if (!_runtimeFactory.TryBuild(
                            _currentTokenData,
                            _currentRunInfo,
                            response.Payload,
                            out var runtimeState,
                            out var buildError))
                    {
                        HandleFatalError(buildError, "bootstrap_build_failed");
                        return;
                    }

                    _persistenceService.SaveAuthContext(runtimeState.AuthToken, runtimeState.AuthenticatedRun);
                    _persistenceService.SaveBootstrapSnapshot(
                        runtimeState.AuthenticatedRun,
                        runtimeState.Bootstrap.BootstrapVersion,
                        response.RawResponse);

                    PublishRuntime(runtimeState);
                    _navigation.ShowSessionReady("bootstrap_loaded");

                    _logger.Info($"Bootstrap load succeeded for run '{runtimeState.AuthenticatedRun.RunId}'.");
                    _userActionLogger.Log(
                        UserActionType.SessionLifecycle,
                        "bootstrap_load_succeeded",
                        BuildSessionMetadata(runtimeState.AuthenticatedRun));
                    return;
                }

                if (allowSnapshotFallback && TryRestoreFromSnapshot(out var restoredRuntimeState, out _))
                {
                    PublishRuntime(restoredRuntimeState);
                    _navigation.ShowSessionReady("bootstrap_snapshot_fallback");

                    _logger.Warning("Bootstrap request failed; local snapshot fallback was used.");
                    _userActionLogger.Log(
                        UserActionType.SessionLifecycle,
                        "bootstrap_snapshot_fallback",
                        BuildSessionMetadata(_currentRunInfo));
                    return;
                }

                var errorMessage = BuildApiError("Не удалось загрузить данные сессии.", response);
                _logger.Warning(errorMessage);
                SetRecoverableError(errorMessage, loginFallbackState, "bootstrap_failed");
            });
        }

        private bool TryRestoreFromSnapshot(out ClientRuntimeState runtimeState, out string error)
        {
            runtimeState = ClientRuntimeState.Empty;
            error = string.Empty;

            if (!_persistenceService.TryLoadBootstrapSnapshot(out var snapshot)
                || snapshot == null)
            {
                error = "Локальный снимок сессии недоступен.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(snapshot.runId)
                && _currentRunInfo.IsValid
                && !string.Equals(snapshot.runId, _currentRunInfo.RunId, StringComparison.Ordinal))
            {
                error = "Локальный снимок относится к другому запуску.";
                return false;
            }

            if (_runtimeFactory.TryBuildFromSerializedBootstrap(
                    _currentTokenData,
                    _currentRunInfo,
                    snapshot.rawBootstrapPayload,
                    out runtimeState,
                    out error))
            {
                _currentTokenData = runtimeState.AuthToken;
                _currentRunInfo = runtimeState.AuthenticatedRun;
                return true;
            }

            _logger.Warning($"Stored bootstrap snapshot could not be restored. {error}");
            return false;
        }

        private void PublishRuntime(ClientRuntimeState runtimeState)
        {
            _currentRuntime = runtimeState ?? ClientRuntimeState.Empty;
            _currentTokenData = _currentRuntime.AuthToken ?? AuthTokenData.Empty;
            _currentRunInfo = _currentRuntime.AuthenticatedRun ?? AuthenticatedRunInfo.Empty;

            if (_currentRuntime.HasSession)
            {
                var metrics = new GameMetricBag();
                metrics.SetNumber("run.currentPeriod", _currentRuntime.Bootstrap.Run.CurrentPeriodNumber);
                metrics.SetText("run.status", _currentRuntime.Bootstrap.Run.RunStatus.ToString());

                _gameSessionService.LoadSession(new GameSessionSnapshot(
                    _currentRuntime.AuthenticatedRun.RunId,
                    true,
                    GameFlowStage.MainMenu,
                    GameFlowPhase.Idle,
                    metrics));

                _stateStore.SetState(state => state.With(
                    statusMessage: string.Empty,
                    lastError: string.Empty));
            }
            else
            {
                _gameSessionService.ResetSession();
            }

            RuntimeChanged?.Invoke(_currentRuntime);
            _eventAggregator.Publish(new EventsProvider.ClientRuntimeStateChangedEvent(_currentRuntime));
        }

        private void SetRecoverableError(
            string message,
            AppStateId fallbackState,
            string reason)
        {
            _stateStore.SetState(state => state.With(lastError: message ?? "Неизвестная ошибка."));
            PublishRuntime(ClientRuntimeState.Empty);
            _navigation.MoveTo(fallbackState, reason);
        }

        private void HandleFatalError(string message, string reason)
        {
            _logger.Error(message);
            _stateStore.SetState(state => state.With(lastError: message ?? "Неизвестная ошибка."));
            PublishRuntime(ClientRuntimeState.Empty);
            _navigation.MoveTo(AppStateId.FatalError, reason);
        }

        private static string BuildApiError<T>(string prefix, Game.Core.Application.Networking.ApiResponse<T> response)
        {
            var detail = response == null
                ? string.Empty
                : string.IsNullOrWhiteSpace(response.Error)
                    ? $"HTTP {response.StatusCode}"
                    : response.StatusCode > 0
                        ? $"HTTP {response.StatusCode}: {response.Error}"
                        : response.Error;

            return string.IsNullOrWhiteSpace(detail)
                ? prefix
                : $"{prefix} {detail}";
        }

        private static Dictionary<string, string> BuildSessionMetadata(AuthenticatedRunInfo runInfo)
        {
            var metadata = new Dictionary<string, string>();

            if (runInfo == null)
            {
                return metadata;
            }

            if (!string.IsNullOrWhiteSpace(runInfo.RunId))
            {
                metadata["runId"] = runInfo.RunId;
            }

            if (!string.IsNullOrWhiteSpace(runInfo.SessionDefinitionCode))
            {
                metadata["sessionCode"] = runInfo.SessionDefinitionCode;
            }

            metadata["runStatus"] = runInfo.RunStatus.ToString();
            metadata["period"] = runInfo.CurrentPeriodNumber.ToString();

            if (!string.IsNullOrWhiteSpace(runInfo.AssignedGroupCode))
            {
                metadata["group"] = runInfo.AssignedGroupCode;
            }

            return metadata;
        }
    }
}
