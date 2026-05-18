using System;
using System.Collections.Generic;
using Game.Core.Application;
using Game.Core.Application.Logging;
using Game.Core.Application.Navigation;
using Game.Core.Application.Periods;
using Game.Core.Application.State;
using Game.Core.Events;
using Game.Domain.GameFlow;

namespace Game.Core.Application.Session
{
    public sealed class SessionFlowCoordinator : ISessionFlowCoordinator
    {
        private const string GameplayStepKeyFormat = "period:{0}:gameplay";

        private readonly ISessionCoordinator _sessionCoordinator;
        private readonly ISessionPersistenceService _persistenceService;
        private readonly IPeriodGameplayService _periodGameplayService;
        private readonly IApplicationStateStore _stateStore;
        private readonly IGameSessionService _gameSessionService;
        private readonly IApplicationNavigationService _navigation;
        private readonly IUserActionLogger _userActionLogger;
        private readonly IAppLogger _logger;
        private readonly IEventAggregator _eventAggregator;

        private SessionFlowRuntimeState _current = SessionFlowRuntimeState.Empty;

        public SessionFlowRuntimeState Current => _current;

        public event Action<SessionFlowRuntimeState> Changed;

        public SessionFlowCoordinator(
            ISessionCoordinator sessionCoordinator,
            ISessionPersistenceService persistenceService,
            IPeriodGameplayService periodGameplayService,
            IApplicationStateStore stateStore,
            IGameSessionService gameSessionService,
            IApplicationNavigationService navigation,
            IUserActionLogger userActionLogger,
            IAppLogger logger,
            IEventAggregator eventAggregator)
        {
            _sessionCoordinator = sessionCoordinator;
            _persistenceService = persistenceService;
            _periodGameplayService = periodGameplayService;
            _stateStore = stateStore;
            _gameSessionService = gameSessionService;
            _navigation = navigation;
            _userActionLogger = userActionLogger;
            _logger = logger;
            _eventAggregator = eventAggregator;

            if (_sessionCoordinator != null)
            {
                _sessionCoordinator.RuntimeChanged += OnClientRuntimeChanged;
            }

            if (_periodGameplayService != null)
            {
                _periodGameplayService.Changed += OnPeriodRuntimeChanged;
            }
        }

        public void StartOrResume()
        {
            var clientRuntime = _sessionCoordinator != null
                ? _sessionCoordinator.CurrentRuntime
                : ClientRuntimeState.Empty;

            if (clientRuntime == null || !clientRuntime.HasSession)
            {
                Publish(SessionFlowRuntimeState.Empty);
                return;
            }

            var config = clientRuntime.Bootstrap != null
                         && clientRuntime.Bootstrap.Session != null
                         && clientRuntime.Bootstrap.Session.SessionConfig != null
                ? clientRuntime.Bootstrap.Session.SessionConfig.Runtime
                : SessionConfigRuntime.Empty;

            if (config == null || !config.IsValid)
            {
                var error = config != null && !string.IsNullOrWhiteSpace(config.ValidationError)
                    ? config.ValidationError
                    : "Сценарий сессии невалиден и не может быть запущен.";
                _logger.Error(error);
                _navigation?.ShowError(error, "session_flow_invalid_config");
                Publish(new SessionFlowRuntimeState(
                    config ?? SessionConfigRuntime.Empty,
                    SessionFlowProgressState.Empty,
                    SessionFlowStepViewModel.Empty,
                    string.Empty,
                    error,
                    false));
                return;
            }

            var progress = LoadOrCreateProgress(clientRuntime, config);
            ResolveAndPublish(clientRuntime, config, progress, string.Empty, string.Empty);
        }

        public void CompleteActiveStep()
        {
            var descriptor = _current != null && _current.Progress != null
                ? _current.Progress.ActiveStep
                : SessionFlowStepDescriptor.Empty;

            if (!descriptor.IsDefined || descriptor.Scope == SessionFlowStepScope.PeriodGameplay)
            {
                return;
            }

            var nextProgress = _current.Progress
                .WithCompleted(descriptor.Scope, descriptor.Key)
                .WithActiveStep(SessionFlowStepDescriptor.Empty, descriptor.PeriodNumber);

            ResolveAndPublish(
                _sessionCoordinator.CurrentRuntime,
                _current.Config,
                nextProgress,
                "Шаг сценария завершён.",
                string.Empty);
        }

        public void SkipActiveStep()
        {
            var activeView = _current != null ? _current.ActiveStepView : SessionFlowStepViewModel.Empty;
            var descriptor = _current != null && _current.Progress != null
                ? _current.Progress.ActiveStep
                : SessionFlowStepDescriptor.Empty;

            if (!descriptor.IsDefined || !activeView.CanSkip)
            {
                return;
            }

            var nextProgress = _current.Progress
                .WithCompleted(descriptor.Scope, descriptor.Key)
                .WithActiveStep(SessionFlowStepDescriptor.Empty, descriptor.PeriodNumber);

            ResolveAndPublish(
                _sessionCoordinator.CurrentRuntime,
                _current.Config,
                nextProgress,
                "Необязательный шаг пропущен.",
                string.Empty);
        }

        public void SubmitActiveSurvey(IReadOnlyDictionary<string, string> answers)
        {
            var activeView = _current != null ? _current.ActiveStepView : SessionFlowStepViewModel.Empty;
            var descriptor = _current != null && _current.Progress != null
                ? _current.Progress.ActiveStep
                : SessionFlowStepDescriptor.Empty;

            if (!descriptor.IsDefined || activeView.RendererKind != SessionFlowRendererKind.Survey)
            {
                return;
            }

            var validationError = ValidateSurveyAnswers(activeView.Questions, answers);

            if (!string.IsNullOrWhiteSpace(validationError))
            {
                Publish(new SessionFlowRuntimeState(
                    _current.Config,
                    _current.Progress,
                    _current.ActiveStepView,
                    string.Empty,
                    validationError,
                    false));
                ApplyScreenState(ScreenId.FlowStep, string.Empty, validationError, false);
                return;
            }

            var nextProgress = _current.Progress
                .WithCompleted(descriptor.Scope, descriptor.Key)
                .WithActiveStep(SessionFlowStepDescriptor.Empty, descriptor.PeriodNumber);

            ResolveAndPublish(
                _sessionCoordinator.CurrentRuntime,
                _current.Config,
                nextProgress,
                "Ответы сохранены локально.",
                string.Empty);
        }

        public void ClearRuntime()
        {
            Publish(SessionFlowRuntimeState.Empty);
            _persistenceService?.ClearSessionFlowProgress();
        }

        private void ResolveAndPublish(
            ClientRuntimeState clientRuntime,
            SessionConfigRuntime config,
            SessionFlowProgressState progress,
            string statusMessage,
            string lastError)
        {
            if (clientRuntime == null || !clientRuntime.HasSession || config == null || !config.IsValid)
            {
                Publish(SessionFlowRuntimeState.Empty);
                return;
            }

            var descriptor = ResolveNextStepDescriptor(clientRuntime, config, progress);

            if (!descriptor.IsDefined)
            {
                var completedProgress = progress
                    .WithPostSessionCompleted(true)
                    .WithSessionCompleted(true)
                    .WithActiveStep(SessionFlowStepDescriptor.Empty, progress.ActivePeriodNumber);

                PersistProgress(clientRuntime, config, completedProgress);
                Publish(new SessionFlowRuntimeState(
                    config,
                    completedProgress,
                    new SessionFlowStepViewModel(
                        new SessionFlowStepDescriptor("completion", SessionFlowStepScope.Completion, 0, SessionFlowStepType.Unknown, "Сессия завершена", true),
                        SessionFlowRendererKind.Completion,
                        "Сессия завершена",
                        string.Empty,
                        "Все этапы сценария пройдены.",
                        true,
                        false,
                        "Продолжить",
                        string.Empty,
                        string.Empty,
                        Array.Empty<SessionFlowQuestionRuntime>()),
                    string.IsNullOrWhiteSpace(statusMessage)
                        ? "Сценарий завершён."
                        : statusMessage,
                    lastError,
                    true));

                _sessionCoordinator?.CompleteRunLocally(
                    string.IsNullOrWhiteSpace(statusMessage)
                        ? "Сценарий завершён."
                        : statusMessage,
                    "session_flow_completed");
                return;
            }

            var nextProgress = progress.WithActiveStep(descriptor, descriptor.PeriodNumber);
            PersistProgress(clientRuntime, config, nextProgress);

            if (descriptor.Scope == SessionFlowStepScope.PeriodGameplay)
            {
                if (descriptor.PeriodNumber > 0
                    && (clientRuntime.Bootstrap == null
                        || clientRuntime.Bootstrap.Run == null
                        || clientRuntime.Bootstrap.Run.CurrentPeriodNumber != descriptor.PeriodNumber))
                {
                    _sessionCoordinator?.UpdateLocalRunProgress(descriptor.PeriodNumber, RunLifecycleStatus.InProgress);
                }

                Publish(new SessionFlowRuntimeState(
                    config,
                    nextProgress,
                    SessionFlowStepViewModel.Empty,
                    string.IsNullOrWhiteSpace(statusMessage)
                        ? "Подготовка периода..."
                        : statusMessage,
                    lastError,
                    false));
                ApplyScreenState(ScreenId.Gameplay, statusMessage, lastError, false);
                _gameSessionService?.SetStage(GameFlowStage.Gameplay);
                _gameSessionService?.SetPhase(GameFlowPhase.LoadingData);
                _periodGameplayService?.ActivateCurrentPeriod();
                return;
            }

            var activeStepView = BuildStepViewModel(clientRuntime, config, descriptor);
            Publish(new SessionFlowRuntimeState(
                config,
                nextProgress,
                activeStepView,
                statusMessage,
                lastError,
                false));
            ApplyScreenState(ScreenId.FlowStep, statusMessage, lastError, false);
            _gameSessionService?.SetStage(GameFlowStage.Gameplay);
            _gameSessionService?.SetPhase(GameFlowPhase.Preparation);
        }

        private SessionFlowProgressState LoadOrCreateProgress(
            ClientRuntimeState clientRuntime,
            SessionConfigRuntime config)
        {
            if (_persistenceService != null
                && _persistenceService.TryLoadSessionFlowProgress(out var snapshot)
                && snapshot != null
                && string.Equals(snapshot.runId, clientRuntime.AuthenticatedRun.RunId, StringComparison.Ordinal)
                && snapshot.sessionConfigVersion == clientRuntime.Bootstrap.Session.ConfigVersion
                && snapshot.schemaVersion == config.SchemaVersion)
            {
                return snapshot.ToProgressState();
            }

            return SessionFlowProgressState.Empty;
        }

        private SessionFlowStepDescriptor ResolveNextStepDescriptor(
            ClientRuntimeState clientRuntime,
            SessionConfigRuntime config,
            SessionFlowProgressState progress)
        {
            var periods = config.Periods ?? Array.Empty<SessionPeriodRuntime>();
            var firstConfiguredPeriod = periods.Count > 0 ? Math.Max(1, periods[0].Number) : 1;
            var runCurrentPeriod = clientRuntime.Bootstrap != null
                                   && clientRuntime.Bootstrap.Run != null
                                   && clientRuntime.Bootstrap.Run.CurrentPeriodNumber > 0
                ? clientRuntime.Bootstrap.Run.CurrentPeriodNumber
                : firstConfiguredPeriod;
            var implicitSkip = !HasMeaningfulProgress(progress);

            if (config.PreSessionFlow != null && config.PreSessionFlow.IsEnabled && config.PreSessionFlow.HasSteps)
            {
                var shouldSkipPreSession = implicitSkip
                                           && (runCurrentPeriod > firstConfiguredPeriod
                                               || clientRuntime.Bootstrap.Run.RunStatus == RunLifecycleStatus.InProgress
                                               || clientRuntime.Bootstrap.Run.RunStatus == RunLifecycleStatus.Completed);

                if (!shouldSkipPreSession)
                {
                    for (var index = 0; index < config.PreSessionFlow.Steps.Count; index++)
                    {
                        var step = config.PreSessionFlow.Steps[index];

                        if (step == null || progress.IsCompleted(SessionFlowStepScope.PreSession, step.Id))
                        {
                            continue;
                        }

                        return new SessionFlowStepDescriptor(
                            step.Id,
                            SessionFlowStepScope.PreSession,
                            0,
                            step.Type,
                            step.Title,
                            step.IsRequired);
                    }
                }
            }

            for (var periodIndex = 0; periodIndex < periods.Count; periodIndex++)
            {
                var period = periods[periodIndex];

                if (period == null)
                {
                    continue;
                }

                var shouldImplicitlySkipPeriod = implicitSkip && period.Number < runCurrentPeriod;

                if (!shouldImplicitlySkipPeriod)
                {
                    for (var blockIndex = 0; blockIndex < period.PeriodContentBlocks.Count; blockIndex++)
                    {
                        var block = period.PeriodContentBlocks[blockIndex];

                        if (block == null || progress.IsCompleted(SessionFlowStepScope.PeriodContent, block.Id))
                        {
                            continue;
                        }

                        return new SessionFlowStepDescriptor(
                            block.Id,
                            SessionFlowStepScope.PeriodContent,
                            period.Number,
                            block.Type,
                            block.Title,
                            block.IsRequired);
                    }

                    if (!progress.IsPeriodCompleted(period.Number))
                    {
                        return new SessionFlowStepDescriptor(
                            string.Format(GameplayStepKeyFormat, period.Number),
                            SessionFlowStepScope.PeriodGameplay,
                            period.Number,
                            SessionFlowStepType.Unknown,
                            string.IsNullOrWhiteSpace(period.Title)
                                ? $"Период {period.Number}"
                                : period.Title,
                            true);
                    }

                    for (var surveyIndex = 0; surveyIndex < period.PostPeriodSurveyRefs.Count; surveyIndex++)
                    {
                        var surveyRef = period.PostPeriodSurveyRefs[surveyIndex];
                        var key = BuildSurveyProgressKey(period.Number, surveyRef, surveyIndex);

                        if (progress.IsCompleted(SessionFlowStepScope.PostPeriodSurvey, key))
                        {
                            continue;
                        }

                        return new SessionFlowStepDescriptor(
                            key,
                            SessionFlowStepScope.PostPeriodSurvey,
                            period.Number,
                            SessionFlowStepType.PostPeriodSurvey,
                            ResolveSurveyTitle(surveyRef, period.Number),
                            surveyRef != null && surveyRef.IsRequired);
                    }

                    for (var interIndex = 0; interIndex < period.InterPeriodBlocks.Count; interIndex++)
                    {
                        var block = period.InterPeriodBlocks[interIndex];

                        if (block == null || progress.IsCompleted(SessionFlowStepScope.InterPeriodBlock, block.Id))
                        {
                            continue;
                        }

                        return new SessionFlowStepDescriptor(
                            block.Id,
                            SessionFlowStepScope.InterPeriodBlock,
                            period.Number,
                            block.Type,
                            block.Title,
                            block.IsRequired);
                    }
                }
            }

            if (config.PostSessionFlow != null && config.PostSessionFlow.IsEnabled && config.PostSessionFlow.HasSteps)
            {
                for (var index = 0; index < config.PostSessionFlow.Steps.Count; index++)
                {
                    var step = config.PostSessionFlow.Steps[index];

                    if (step == null || progress.IsCompleted(SessionFlowStepScope.PostSession, step.Id))
                    {
                        continue;
                    }

                    return new SessionFlowStepDescriptor(
                        step.Id,
                        SessionFlowStepScope.PostSession,
                        0,
                        step.Type,
                        step.Title,
                        step.IsRequired);
                }
            }

            return SessionFlowStepDescriptor.Empty;
        }

        private SessionFlowStepViewModel BuildStepViewModel(
            ClientRuntimeState clientRuntime,
            SessionConfigRuntime config,
            SessionFlowStepDescriptor descriptor)
        {
            switch (descriptor.Scope)
            {
                case SessionFlowStepScope.PreSession:
                case SessionFlowStepScope.PostSession:
                    return BuildFlowStepViewModel(
                        config,
                        clientRuntime,
                        descriptor,
                        ResolveFlowStep(config, descriptor.Scope, descriptor.Key));
                case SessionFlowStepScope.PeriodContent:
                    return BuildPeriodContentViewModel(
                        config,
                        descriptor,
                        ResolvePeriodContentBlock(config, descriptor.PeriodNumber, descriptor.Key));
                case SessionFlowStepScope.InterPeriodBlock:
                    return BuildInterPeriodBlockViewModel(
                        config,
                        descriptor,
                        ResolveInterPeriodBlock(config, descriptor.PeriodNumber, descriptor.Key));
                case SessionFlowStepScope.PostPeriodSurvey:
                    return BuildSurveyViewModel(
                        clientRuntime,
                        config,
                        descriptor,
                        ResolvePostPeriodSurvey(config, descriptor.PeriodNumber, descriptor.Key));
                default:
                    return SessionFlowStepViewModel.Empty;
            }
        }

        private SessionFlowStepViewModel BuildFlowStepViewModel(
            SessionConfigRuntime config,
            ClientRuntimeState clientRuntime,
            SessionFlowStepDescriptor descriptor,
            FlowStepRuntime step)
        {
            if (step == null || string.IsNullOrWhiteSpace(step.Id))
            {
                return BuildMissingStepViewModel(descriptor, "Не удалось найти шаг сценария.");
            }

            if (IsSurveyType(step.Type))
            {
                return BuildSurveyViewModel(
                    clientRuntime,
                    config,
                    descriptor,
                    ResolveSurveyRef(step.SurveyRef, config));
            }

            return new SessionFlowStepViewModel(
                descriptor,
                SessionFlowRendererKind.Content,
                ResolveTitle(step.Title, descriptor.Title, "Инструкция"),
                step.Subtitle,
                step.Body,
                step.IsRequired,
                !step.IsRequired && CanSkipOptionalSteps(config),
                "Далее",
                "Пропустить",
                string.Empty,
                Array.Empty<SessionFlowQuestionRuntime>());
        }

        private SessionFlowStepViewModel BuildPeriodContentViewModel(
            SessionConfigRuntime config,
            SessionFlowStepDescriptor descriptor,
            PeriodContentBlockRuntime block)
        {
            if (block == null || string.IsNullOrWhiteSpace(block.Id))
            {
                return BuildMissingStepViewModel(descriptor, "Не удалось найти блок периода.");
            }

            return new SessionFlowStepViewModel(
                descriptor,
                SessionFlowRendererKind.Content,
                ResolveTitle(block.Title, descriptor.Title, $"Период {descriptor.PeriodNumber}"),
                block.Subtitle,
                block.Body,
                block.IsRequired,
                !block.IsRequired && CanSkipOptionalSteps(config),
                "Далее",
                "Пропустить",
                string.Empty,
                Array.Empty<SessionFlowQuestionRuntime>());
        }

        private SessionFlowStepViewModel BuildInterPeriodBlockViewModel(
            SessionConfigRuntime config,
            SessionFlowStepDescriptor descriptor,
            InterPeriodBlockRuntime block)
        {
            if (block == null || string.IsNullOrWhiteSpace(block.Id))
            {
                return BuildMissingStepViewModel(descriptor, "Не удалось найти межпериодный блок.");
            }

            return new SessionFlowStepViewModel(
                descriptor,
                SessionFlowRendererKind.Content,
                ResolveTitle(block.Title, descriptor.Title, "Межпериодный блок"),
                block.Subtitle,
                block.Body,
                block.IsRequired,
                !block.IsRequired && CanSkipOptionalSteps(config),
                "Далее",
                "Пропустить",
                string.Empty,
                Array.Empty<SessionFlowQuestionRuntime>());
        }

        private SessionFlowStepViewModel BuildSurveyViewModel(
            ClientRuntimeState clientRuntime,
            SessionConfigRuntime config,
            SessionFlowStepDescriptor descriptor,
            SurveyRefRuntime surveyRef)
        {
            if (surveyRef == null || surveyRef.IsEmpty)
            {
                return BuildMissingStepViewModel(descriptor, "Ссылка на анкету не найдена.");
            }

            var resolvedSurvey = ResolveSurveyRef(surveyRef, config);

            if (!TryResolveSurveyTemplate(clientRuntime, resolvedSurvey, out var template))
            {
                var error = string.IsNullOrWhiteSpace(resolvedSurvey.TemplateCode)
                    ? "Шаблон анкеты не найден в bootstrap."
                    : $"Шаблон анкеты '{resolvedSurvey.TemplateCode}' не найден в bootstrap.";
                _logger.Warning(error);

                return new SessionFlowStepViewModel(
                    descriptor,
                    SessionFlowRendererKind.Content,
                    ResolveTitle(resolvedSurvey.Title, descriptor.Title, "Анкета"),
                    string.Empty,
                    error,
                    resolvedSurvey.IsRequired,
                    !resolvedSurvey.IsRequired && CanSkipOptionalSteps(config),
                    "Далее",
                    "Пропустить",
                    error,
                    Array.Empty<SessionFlowQuestionRuntime>());
            }

            var questions = BuildSurveyQuestions(template);
            var body = template.TemplateDocument != null
                ? ResolveTemplateBody(template.TemplateDocument.Root)
                : string.Empty;

            return new SessionFlowStepViewModel(
                descriptor,
                SessionFlowRendererKind.Survey,
                ResolveTitle(resolvedSurvey.Title, template.Title, "Анкета"),
                resolvedSurvey.Purpose,
                body,
                resolvedSurvey.IsRequired,
                !resolvedSurvey.IsRequired && CanSkipOptionalSteps(config),
                "Отправить",
                "Пропустить",
                string.Empty,
                questions);
        }

        private static IReadOnlyList<SessionFlowQuestionRuntime> BuildSurveyQuestions(SurveyTemplateRuntimeModel template)
        {
            if (template == null
                || template.TemplateDocument == null
                || !template.TemplateDocument.IsValid
                || template.TemplateDocument.Root.Kind == JsonValueKind.Null)
            {
                return Array.Empty<SessionFlowQuestionRuntime>();
            }

            var questionsNode = template.TemplateDocument.Root.FindFirstDescendantProperty("questions", "items", "fields");

            if (questionsNode.Kind != JsonValueKind.Array)
            {
                return Array.Empty<SessionFlowQuestionRuntime>();
            }

            var result = new List<SessionFlowQuestionRuntime>(questionsNode.Count);

            for (var index = 0; index < questionsNode.ArrayValue.Count; index++)
            {
                var questionNode = questionsNode.ArrayValue[index];

                if (questionNode == null || questionNode.Kind != JsonValueKind.Object)
                {
                    continue;
                }

                var id = ReadString(questionNode, "id", "code", "key");
                var label = ReadString(questionNode, "label", "title", "question", "text", "name");

                if (string.IsNullOrWhiteSpace(id))
                {
                    id = $"question_{index + 1}";
                }

                if (string.IsNullOrWhiteSpace(label))
                {
                    label = $"Вопрос {index + 1}";
                }

                var optionsNode = questionNode.FindFirstProperty("options", "choices", "answers", "variants");
                var questionType = ResolveQuestionType(questionNode, optionsNode);
                var options = BuildQuestionOptions(optionsNode);

                result.Add(new SessionFlowQuestionRuntime(
                    id,
                    label,
                    ReadString(questionNode, "placeholder", "hint"),
                    ReadString(questionNode, "description", "helpText"),
                    ReadBoolean(questionNode, "required", "isRequired", "mandatory") ?? true,
                    questionType,
                    options));
            }

            return result;
        }

        private static SessionFlowQuestionType ResolveQuestionType(JsonValue questionNode, JsonValue optionsNode)
        {
            var rawType = ReadString(questionNode, "type", "questionType", "kind");
            var rawDisplay = ReadString(questionNode, "display", "layout", "renderer", "presentation");
            var allowMultiple = ReadBoolean(questionNode, "allowMultiple", "multiple", "isMultiple", "multiSelect") == true;

            if (allowMultiple
                || string.Equals(rawType, "multiple", StringComparison.OrdinalIgnoreCase)
                || string.Equals(rawType, "multi", StringComparison.OrdinalIgnoreCase)
                || string.Equals(rawType, "multiselect", StringComparison.OrdinalIgnoreCase)
                || string.Equals(rawType, "checkbox", StringComparison.OrdinalIgnoreCase))
            {
                return SessionFlowQuestionType.MultipleChoice;
            }

            if (string.Equals(rawType, "scale", StringComparison.OrdinalIgnoreCase)
                || string.Equals(rawDisplay, "scale", StringComparison.OrdinalIgnoreCase)
                || string.Equals(rawDisplay, "rating", StringComparison.OrdinalIgnoreCase)
                || string.Equals(rawDisplay, "horizontal_scale", StringComparison.OrdinalIgnoreCase))
            {
                return SessionFlowQuestionType.Scale;
            }

            if (string.Equals(rawType, "single", StringComparison.OrdinalIgnoreCase)
                || string.Equals(rawType, "choice", StringComparison.OrdinalIgnoreCase)
                || string.Equals(rawType, "radio", StringComparison.OrdinalIgnoreCase))
            {
                return SessionFlowQuestionType.SingleChoice;
            }

            if (string.Equals(rawType, "text", StringComparison.OrdinalIgnoreCase)
                || string.Equals(rawType, "string", StringComparison.OrdinalIgnoreCase)
                || string.Equals(rawType, "open", StringComparison.OrdinalIgnoreCase)
                || string.Equals(rawType, "textarea", StringComparison.OrdinalIgnoreCase))
            {
                return SessionFlowQuestionType.Text;
            }

            return optionsNode != null && optionsNode.Kind == JsonValueKind.Array && optionsNode.Count > 0
                ? SessionFlowQuestionType.SingleChoice
                : SessionFlowQuestionType.Text;
        }

        private static IReadOnlyList<SessionFlowQuestionOptionRuntime> BuildQuestionOptions(JsonValue optionsNode)
        {
            if (optionsNode == null || optionsNode.Kind != JsonValueKind.Array || optionsNode.Count == 0)
            {
                return Array.Empty<SessionFlowQuestionOptionRuntime>();
            }

            var result = new List<SessionFlowQuestionOptionRuntime>(optionsNode.Count);

            for (var index = 0; index < optionsNode.ArrayValue.Count; index++)
            {
                var optionNode = optionsNode.ArrayValue[index];

                if (optionNode == null || optionNode.Kind == JsonValueKind.Null)
                {
                    continue;
                }

                if (optionNode.Kind == JsonValueKind.String)
                {
                    var rawLabel = optionNode.GetStringOrDefault().Trim();

                    if (string.IsNullOrWhiteSpace(rawLabel))
                    {
                        continue;
                    }

                    var isOther = IsOtherOption(optionNode, rawLabel, string.Empty);
                    var label = isOther ? "Другое" : rawLabel;
                    var optionId = isOther ? "other" : rawLabel;
                    result.Add(new SessionFlowQuestionOptionRuntime(optionId, label, isOther));
                    continue;
                }

                if (optionNode.Kind != JsonValueKind.Object)
                {
                    continue;
                }

                var optionIdValue = ReadString(optionNode, "id", "code", "key", "value");
                var labelValue = ReadString(optionNode, "label", "title", "text", "name", "value");
                var isOtherObject = IsOtherOption(optionNode, labelValue, optionIdValue);

                if (string.IsNullOrWhiteSpace(labelValue))
                {
                    labelValue = isOtherObject
                        ? "Другое"
                        : $"Вариант {index + 1}";
                }

                if (string.IsNullOrWhiteSpace(optionIdValue))
                {
                    optionIdValue = isOtherObject
                        ? "other"
                        : labelValue;
                }

                result.Add(new SessionFlowQuestionOptionRuntime(
                    optionIdValue,
                    labelValue,
                    isOtherObject));
            }

            return result;
        }

        private static bool IsOtherOption(JsonValue optionNode, string labelValue, string optionIdValue)
        {
            if (optionNode != null && optionNode.Kind == JsonValueKind.Object)
            {
                if (ReadBoolean(optionNode, "isOther", "other", "allowText", "freeText") == true)
                {
                    return true;
                }

                var rawType = ReadString(optionNode, "type", "kind");

                if (string.Equals(rawType, "other", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return IsOtherToken(optionIdValue) || IsOtherToken(labelValue);
        }

        private static bool IsOtherToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var normalized = value.Trim();
            return string.Equals(normalized, "__other__", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalized, "other", StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveTemplateBody(JsonValue root)
        {
            if (root == null || root.Kind == JsonValueKind.Null)
            {
                return string.Empty;
            }

            var direct = ReadString(root, "body", "description", "intro", "text");

            if (!string.IsNullOrWhiteSpace(direct))
            {
                return direct;
            }

            if (root.TryGetPropertyIgnoreCase("content", out var contentNode))
            {
                return ReadString(contentNode, "body", "description", "text");
            }

            return string.Empty;
        }

        private static string ValidateSurveyAnswers(
            IReadOnlyList<SessionFlowQuestionRuntime> questions,
            IReadOnlyDictionary<string, string> answers)
        {
            if (questions == null || questions.Count == 0)
            {
                return string.Empty;
            }

            for (var index = 0; index < questions.Count; index++)
            {
                var question = questions[index];

                if (question == null || !question.IsRequired)
                {
                    continue;
                }

                if (answers == null
                    || !answers.TryGetValue(question.Id, out var value)
                    || string.IsNullOrWhiteSpace(value))
                {
                    return $"Заполните поле «{question.Label}».";
                }
            }

            return string.Empty;
        }

        private static bool TryResolveSurveyTemplate(
            ClientRuntimeState clientRuntime,
            SurveyRefRuntime surveyRef,
            out SurveyTemplateRuntimeModel template)
        {
            template = null;

            if (clientRuntime == null
                || !clientRuntime.HasSession
                || clientRuntime.Bootstrap == null
                || clientRuntime.Bootstrap.SurveyTemplates == null
                || surveyRef == null)
            {
                return false;
            }

            for (var index = 0; index < clientRuntime.Bootstrap.SurveyTemplates.Count; index++)
            {
                var candidate = clientRuntime.Bootstrap.SurveyTemplates[index];

                if (candidate == null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(surveyRef.TemplateCode)
                    && string.Equals(candidate.Code, surveyRef.TemplateCode, StringComparison.OrdinalIgnoreCase))
                {
                    template = candidate;
                    return true;
                }

                if (surveyRef.TemplateId.HasValue && candidate.Id == surveyRef.TemplateId.Value)
                {
                    template = candidate;
                    return true;
                }
            }

            return false;
        }

        private static FlowStepRuntime ResolveFlowStep(
            SessionConfigRuntime config,
            SessionFlowStepScope scope,
            string key)
        {
            var flow = scope == SessionFlowStepScope.PreSession
                ? config.PreSessionFlow
                : config.PostSessionFlow;

            if (flow == null || flow.Steps == null || string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            for (var index = 0; index < flow.Steps.Count; index++)
            {
                var step = flow.Steps[index];

                if (step != null && string.Equals(step.Id, key, StringComparison.Ordinal))
                {
                    return step;
                }
            }

            return null;
        }

        private static PeriodContentBlockRuntime ResolvePeriodContentBlock(
            SessionConfigRuntime config,
            int periodNumber,
            string key)
        {
            return config.TryGetPeriod(periodNumber, out var period)
                ? FindByKey(period.PeriodContentBlocks, key)
                : null;
        }

        private static InterPeriodBlockRuntime ResolveInterPeriodBlock(
            SessionConfigRuntime config,
            int periodNumber,
            string key)
        {
            return config.TryGetPeriod(periodNumber, out var period)
                ? FindByKey(period.InterPeriodBlocks, key)
                : null;
        }

        private static SurveyRefRuntime ResolvePostPeriodSurvey(
            SessionConfigRuntime config,
            int periodNumber,
            string key)
        {
            if (!config.TryGetPeriod(periodNumber, out var period) || period == null)
            {
                return SurveyRefRuntime.Empty;
            }

            for (var index = 0; index < period.PostPeriodSurveyRefs.Count; index++)
            {
                var surveyRef = period.PostPeriodSurveyRefs[index];
                var candidateKey = BuildSurveyProgressKey(periodNumber, surveyRef, index);

                if (string.Equals(candidateKey, key, StringComparison.Ordinal))
                {
                    return surveyRef;
                }
            }

            return SurveyRefRuntime.Empty;
        }

        private static T FindByKey<T>(IReadOnlyList<T> items, string key)
            where T : class
        {
            if (items == null || string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            for (var index = 0; index < items.Count; index++)
            {
                var item = items[index];

                switch (item)
                {
                    case PeriodContentBlockRuntime contentBlock when string.Equals(contentBlock.Id, key, StringComparison.Ordinal):
                        return item;
                    case InterPeriodBlockRuntime interBlock when string.Equals(interBlock.Id, key, StringComparison.Ordinal):
                        return item;
                }
            }

            return null;
        }

        private SurveyRefRuntime ResolveSurveyRef(SurveyRefRuntime surveyRef, SessionConfigRuntime config)
        {
            if (surveyRef == null || surveyRef.IsEmpty || config == null)
            {
                return surveyRef ?? SurveyRefRuntime.Empty;
            }

            if (!string.IsNullOrWhiteSpace(surveyRef.SharedRefId)
                && config.TryGetSharedSurveyRef(surveyRef.SharedRefId, out var sharedSurveyRef)
                && sharedSurveyRef != null
                && sharedSurveyRef.IsValid)
            {
                return surveyRef.Merge(sharedSurveyRef.Survey);
            }

            return surveyRef;
        }

        private static string ResolveSurveyTitle(SurveyRefRuntime surveyRef, int periodNumber)
        {
            if (surveyRef != null && !string.IsNullOrWhiteSpace(surveyRef.Title))
            {
                return surveyRef.Title;
            }

            return $"Анкета после периода {periodNumber}";
        }

        private static string BuildSurveyProgressKey(int periodNumber, SurveyRefRuntime surveyRef, int index)
        {
            if (surveyRef != null)
            {
                if (!string.IsNullOrWhiteSpace(surveyRef.Id))
                {
                    return $"period_{periodNumber}_survey:{surveyRef.Id}";
                }

                if (!string.IsNullOrWhiteSpace(surveyRef.TemplateCode))
                {
                    return $"period_{periodNumber}_survey:{surveyRef.TemplateCode}";
                }
            }

            return $"period_{periodNumber}_survey:{index + 1}";
        }

        private static bool IsSurveyType(SessionFlowStepType type)
        {
            switch (type)
            {
                case SessionFlowStepType.InstructionQuiz:
                case SessionFlowStepType.PreTest:
                case SessionFlowStepType.PostTest:
                case SessionFlowStepType.PostPeriodSurvey:
                    return true;
                default:
                    return false;
            }
        }

        private static bool CanSkipOptionalSteps(SessionConfigRuntime config)
        {
            return config == null
                   || config.GlobalSettings == null
                   || !config.GlobalSettings.AllowOptionalSkip.HasValue
                   || config.GlobalSettings.AllowOptionalSkip.Value;
        }

        private static bool HasMeaningfulProgress(SessionFlowProgressState progress)
        {
            if (progress == null)
            {
                return false;
            }

            return progress.CompletedPreSessionStepKeys.Count > 0
                   || progress.CompletedPeriodContentBlockKeys.Count > 0
                   || progress.CompletedPostPeriodSurveyKeys.Count > 0
                   || progress.CompletedInterPeriodBlockKeys.Count > 0
                   || progress.CompletedPostSessionStepKeys.Count > 0
                   || progress.CompletedPeriodNumbers.Count > 0
                   || progress.ActiveStep.IsDefined
                   || progress.IsPostSessionCompleted
                   || progress.IsSessionCompleted;
        }

        private void PersistProgress(
            ClientRuntimeState clientRuntime,
            SessionConfigRuntime config,
            SessionFlowProgressState progress)
        {
            if (_persistenceService == null || clientRuntime == null || !clientRuntime.HasSession || progress == null)
            {
                return;
            }

            _persistenceService.SaveSessionFlowProgress(
                clientRuntime.AuthenticatedRun.RunId,
                clientRuntime.Bootstrap.Session.ConfigVersion,
                config != null ? config.SchemaVersion : 0,
                progress);
        }

        private void ApplyScreenState(
            ScreenId screenId,
            string statusMessage,
            string lastError,
            bool isBusy)
        {
            _stateStore?.SetState(state => state.With(
                currentScreen: screenId,
                statusMessage: statusMessage ?? string.Empty,
                lastError: lastError ?? string.Empty,
                isBusy: isBusy));
        }

        private void Publish(SessionFlowRuntimeState nextState)
        {
            _current = nextState ?? SessionFlowRuntimeState.Empty;
            Changed?.Invoke(_current);
            _eventAggregator?.Publish(new EventsProvider.SessionFlowRuntimeChangedEvent(_current));
        }

        private void OnClientRuntimeChanged(ClientRuntimeState runtimeState)
        {
            if (runtimeState == null || !runtimeState.HasSession)
            {
                Publish(SessionFlowRuntimeState.Empty);
                _persistenceService?.ClearSessionFlowProgress();
            }
        }

        private void OnPeriodRuntimeChanged(PeriodRuntimeState runtimeState)
        {
            if (runtimeState == null
                || !_current.Progress.ActiveStep.IsDefined
                || _current.Progress.ActiveStep.Scope != SessionFlowStepScope.PeriodGameplay
                || _current.Progress.ActiveStep.PeriodNumber != runtimeState.PeriodNumber
                || !runtimeState.IsCheckpointSubmitted
                || runtimeState.FlowState != PeriodFlowState.PeriodClosed)
            {
                return;
            }

            var nextProgress = _current.Progress
                .WithCompletedPeriod(runtimeState.PeriodNumber)
                .WithActiveStep(SessionFlowStepDescriptor.Empty, runtimeState.PeriodNumber);

            ResolveAndPublish(
                _sessionCoordinator.CurrentRuntime,
                _current.Config,
                nextProgress,
                "Период завершён.",
                string.Empty);
        }

        private static string ResolveTitle(string primary, string fallback, string defaultTitle)
        {
            if (!string.IsNullOrWhiteSpace(primary))
            {
                return primary;
            }

            if (!string.IsNullOrWhiteSpace(fallback))
            {
                return fallback;
            }

            return defaultTitle;
        }

        private static SessionFlowStepViewModel BuildMissingStepViewModel(
            SessionFlowStepDescriptor descriptor,
            string error)
        {
            return new SessionFlowStepViewModel(
                descriptor,
                SessionFlowRendererKind.Content,
                string.IsNullOrWhiteSpace(descriptor.Title) ? "Ошибка сценария" : descriptor.Title,
                string.Empty,
                error,
                descriptor.IsRequired,
                !descriptor.IsRequired,
                "Далее",
                "Пропустить",
                error,
                Array.Empty<SessionFlowQuestionRuntime>());
        }

        private static string ReadString(JsonValue node, params string[] propertyNames)
        {
            if (node == null || propertyNames == null)
            {
                return string.Empty;
            }

            foreach (var propertyName in propertyNames)
            {
                if (node.TryGetPropertyIgnoreCase(propertyName, out var value))
                {
                    return value.GetStringOrDefault();
                }
            }

            return string.Empty;
        }

        private static bool? ReadBoolean(JsonValue node, params string[] propertyNames)
        {
            if (node == null || propertyNames == null)
            {
                return null;
            }

            foreach (var propertyName in propertyNames)
            {
                if (node.TryGetPropertyIgnoreCase(propertyName, out var value))
                {
                    return value.AsNullableBoolean();
                }
            }

            return null;
        }
    }
}
