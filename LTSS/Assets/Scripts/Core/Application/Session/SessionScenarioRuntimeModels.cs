using System;
using System.Collections.Generic;

namespace Game.Core.Application.Session
{
    public enum SessionFlowStepType
    {
        Unknown,
        Instruction,
        InstructionQuiz,
        PreTest,
        PostTest,
        PostPeriodSurvey,
        InstructionalPopup,
        News
    }

    public enum SessionFlowStepScope
    {
        None,
        PreSession,
        PeriodContent,
        PeriodGameplay,
        PostPeriodSurvey,
        InterPeriodBlock,
        PostSession,
        Completion
    }

    public enum SessionFlowRendererKind
    {
        None,
        Content,
        Survey,
        Completion
    }

    public sealed class GlobalSettingsRuntime
    {
        public static GlobalSettingsRuntime Empty { get; } = new GlobalSettingsRuntime(
            JsonValue.Null,
            "ECU",
            null,
            null);

        public JsonValue RawNode { get; }
        public string CurrencyCode { get; }
        public double? BaseIncomeEcu { get; }
        public bool? AllowOptionalSkip { get; }

        public GlobalSettingsRuntime(
            JsonValue rawNode,
            string currencyCode,
            double? baseIncomeEcu,
            bool? allowOptionalSkip)
        {
            RawNode = rawNode ?? JsonValue.Null;
            CurrencyCode = string.IsNullOrWhiteSpace(currencyCode)
                ? "ECU"
                : currencyCode.Trim();
            BaseIncomeEcu = baseIncomeEcu;
            AllowOptionalSkip = allowOptionalSkip;
        }
    }

    public sealed class SessionFlowRuntime
    {
        public static SessionFlowRuntime Empty { get; } = new SessionFlowRuntime(
            false,
            string.Empty,
            Array.Empty<FlowStepRuntime>());

        public bool IsEnabled { get; }
        public string Title { get; }
        public IReadOnlyList<FlowStepRuntime> Steps { get; }
        public bool HasSteps => Steps != null && Steps.Count > 0;

        public SessionFlowRuntime(
            bool isEnabled,
            string title,
            IReadOnlyList<FlowStepRuntime> steps)
        {
            IsEnabled = isEnabled;
            Title = title ?? string.Empty;
            Steps = steps != null
                ? new List<FlowStepRuntime>(steps)
                : Array.Empty<FlowStepRuntime>();
        }
    }

    public sealed class SurveyRefRuntime
    {
        public static SurveyRefRuntime Empty { get; } = new SurveyRefRuntime(
            string.Empty,
            string.Empty,
            string.Empty,
            null,
            string.Empty,
            string.Empty,
            true);

        public string Id { get; }
        public string SharedRefId { get; }
        public string TemplateCode { get; }
        public int? TemplateId { get; }
        public string Purpose { get; }
        public string Title { get; }
        public bool IsRequired { get; }
        public bool IsEmpty =>
            string.IsNullOrWhiteSpace(Id)
            && string.IsNullOrWhiteSpace(SharedRefId)
            && string.IsNullOrWhiteSpace(TemplateCode)
            && !TemplateId.HasValue;

        public SurveyRefRuntime(
            string id,
            string sharedRefId,
            string templateCode,
            int? templateId,
            string purpose,
            string title,
            bool isRequired)
        {
            Id = id ?? string.Empty;
            SharedRefId = sharedRefId ?? string.Empty;
            TemplateCode = templateCode ?? string.Empty;
            TemplateId = templateId;
            Purpose = purpose ?? string.Empty;
            Title = title ?? string.Empty;
            IsRequired = isRequired;
        }

        public SurveyRefRuntime Merge(SurveyRefRuntime fallback)
        {
            if (fallback == null || fallback.IsEmpty)
            {
                return this;
            }

            return new SurveyRefRuntime(
                string.IsNullOrWhiteSpace(Id) ? fallback.Id : Id,
                string.IsNullOrWhiteSpace(SharedRefId) ? fallback.SharedRefId : SharedRefId,
                string.IsNullOrWhiteSpace(TemplateCode) ? fallback.TemplateCode : TemplateCode,
                TemplateId ?? fallback.TemplateId,
                string.IsNullOrWhiteSpace(Purpose) ? fallback.Purpose : Purpose,
                string.IsNullOrWhiteSpace(Title) ? fallback.Title : Title,
                IsRequired || fallback.IsRequired);
        }
    }

    public sealed class SharedSurveyRefRuntime
    {
        public static SharedSurveyRefRuntime Empty { get; } = new SharedSurveyRefRuntime(
            string.Empty,
            SurveyRefRuntime.Empty);

        public string Id { get; }
        public SurveyRefRuntime Survey { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Id) && Survey != null && !Survey.IsEmpty;

        public SharedSurveyRefRuntime(string id, SurveyRefRuntime survey)
        {
            Id = id ?? string.Empty;
            Survey = survey ?? SurveyRefRuntime.Empty;
        }
    }

    public sealed class FlowStepRuntime
    {
        public static FlowStepRuntime Empty { get; } = new FlowStepRuntime(
            string.Empty,
            SessionFlowStepType.Unknown,
            string.Empty,
            string.Empty,
            string.Empty,
            true,
            string.Empty,
            SurveyRefRuntime.Empty,
            JsonValue.Null);

        public string Id { get; }
        public SessionFlowStepType Type { get; }
        public string Title { get; }
        public string Subtitle { get; }
        public string Body { get; }
        public bool IsRequired { get; }
        public string InternalCode { get; }
        public SurveyRefRuntime SurveyRef { get; }
        public JsonValue RawNode { get; }

        public FlowStepRuntime(
            string id,
            SessionFlowStepType type,
            string title,
            string subtitle,
            string body,
            bool isRequired,
            string internalCode,
            SurveyRefRuntime surveyRef,
            JsonValue rawNode)
        {
            Id = id ?? string.Empty;
            Type = type;
            Title = title ?? string.Empty;
            Subtitle = subtitle ?? string.Empty;
            Body = body ?? string.Empty;
            IsRequired = isRequired;
            InternalCode = internalCode ?? string.Empty;
            SurveyRef = surveyRef ?? SurveyRefRuntime.Empty;
            RawNode = rawNode ?? JsonValue.Null;
        }
    }

    public sealed class PeriodInfoEntryRuntime
    {
        public static PeriodInfoEntryRuntime Empty { get; } = new PeriodInfoEntryRuntime(
            string.Empty,
            string.Empty,
            null,
            string.Empty,
            string.Empty);

        public string Id { get; }
        public string Label { get; }
        public double? NumericValue { get; }
        public string RawText { get; }
        public string Suffix { get; }
        public bool HasValue => NumericValue.HasValue || !string.IsNullOrWhiteSpace(RawText);

        public PeriodInfoEntryRuntime(
            string id,
            string label,
            double? numericValue,
            string rawText,
            string suffix)
        {
            Id = id ?? string.Empty;
            Label = label ?? string.Empty;
            NumericValue = numericValue;
            RawText = rawText ?? string.Empty;
            Suffix = suffix ?? string.Empty;
        }
    }

    public sealed class PeriodInfoBlockRuntime
    {
        public static PeriodInfoBlockRuntime Empty { get; } = new PeriodInfoBlockRuntime(
            Array.Empty<PeriodInfoEntryRuntime>(),
            JsonValue.Null);

        public IReadOnlyList<PeriodInfoEntryRuntime> Entries { get; }
        public JsonValue RawNode { get; }

        public PeriodInfoBlockRuntime(
            IReadOnlyList<PeriodInfoEntryRuntime> entries,
            JsonValue rawNode)
        {
            Entries = entries != null
                ? new List<PeriodInfoEntryRuntime>(entries)
                : Array.Empty<PeriodInfoEntryRuntime>();
            RawNode = rawNode ?? JsonValue.Null;
        }
    }

    public sealed class PeriodContentBlockRuntime
    {
        public static PeriodContentBlockRuntime Empty { get; } = new PeriodContentBlockRuntime(
            string.Empty,
            SessionFlowStepType.Unknown,
            string.Empty,
            string.Empty,
            string.Empty,
            true,
            string.Empty,
            JsonValue.Null);

        public string Id { get; }
        public SessionFlowStepType Type { get; }
        public string Title { get; }
        public string Subtitle { get; }
        public string Body { get; }
        public bool IsRequired { get; }
        public string InternalCode { get; }
        public JsonValue RawNode { get; }

        public PeriodContentBlockRuntime(
            string id,
            SessionFlowStepType type,
            string title,
            string subtitle,
            string body,
            bool isRequired,
            string internalCode,
            JsonValue rawNode)
        {
            Id = id ?? string.Empty;
            Type = type;
            Title = title ?? string.Empty;
            Subtitle = subtitle ?? string.Empty;
            Body = body ?? string.Empty;
            IsRequired = isRequired;
            InternalCode = internalCode ?? string.Empty;
            RawNode = rawNode ?? JsonValue.Null;
        }
    }

    public sealed class InterPeriodBlockRuntime
    {
        public static InterPeriodBlockRuntime Empty { get; } = new InterPeriodBlockRuntime(
            string.Empty,
            SessionFlowStepType.Unknown,
            string.Empty,
            string.Empty,
            string.Empty,
            true,
            string.Empty,
            JsonValue.Null);

        public string Id { get; }
        public SessionFlowStepType Type { get; }
        public string Title { get; }
        public string Subtitle { get; }
        public string Body { get; }
        public bool IsRequired { get; }
        public string InternalCode { get; }
        public JsonValue RawNode { get; }

        public InterPeriodBlockRuntime(
            string id,
            SessionFlowStepType type,
            string title,
            string subtitle,
            string body,
            bool isRequired,
            string internalCode,
            JsonValue rawNode)
        {
            Id = id ?? string.Empty;
            Type = type;
            Title = title ?? string.Empty;
            Subtitle = subtitle ?? string.Empty;
            Body = body ?? string.Empty;
            IsRequired = isRequired;
            InternalCode = internalCode ?? string.Empty;
            RawNode = rawNode ?? JsonValue.Null;
        }
    }

    public sealed class SessionPeriodRuntime
    {
        public static SessionPeriodRuntime Empty { get; } = new SessionPeriodRuntime(
            0,
            string.Empty,
            string.Empty,
            string.Empty,
            Array.Empty<string>(),
            PeriodInfoBlockRuntime.Empty,
            Array.Empty<PeriodContentBlockRuntime>(),
            Array.Empty<SurveyRefRuntime>(),
            Array.Empty<InterPeriodBlockRuntime>(),
            string.Empty,
            JsonValue.Null);

        public int Number { get; }
        public string Title { get; }
        public string HistoricalYear { get; }
        public string Phase { get; }
        public IReadOnlyList<string> EnabledFeatures { get; }
        public PeriodInfoBlockRuntime InfoBlock { get; }
        public IReadOnlyList<PeriodContentBlockRuntime> PeriodContentBlocks { get; }
        public IReadOnlyList<SurveyRefRuntime> PostPeriodSurveyRefs { get; }
        public IReadOnlyList<InterPeriodBlockRuntime> InterPeriodBlocks { get; }
        public string InternalCode { get; }
        public JsonValue RawNode { get; }

        public SessionPeriodRuntime(
            int number,
            string title,
            string historicalYear,
            string phase,
            IReadOnlyList<string> enabledFeatures,
            PeriodInfoBlockRuntime infoBlock,
            IReadOnlyList<PeriodContentBlockRuntime> periodContentBlocks,
            IReadOnlyList<SurveyRefRuntime> postPeriodSurveyRefs,
            IReadOnlyList<InterPeriodBlockRuntime> interPeriodBlocks,
            string internalCode,
            JsonValue rawNode)
        {
            Number = number;
            Title = title ?? string.Empty;
            HistoricalYear = historicalYear ?? string.Empty;
            Phase = phase ?? string.Empty;
            EnabledFeatures = enabledFeatures != null
                ? new List<string>(enabledFeatures)
                : Array.Empty<string>();
            InfoBlock = infoBlock ?? PeriodInfoBlockRuntime.Empty;
            PeriodContentBlocks = periodContentBlocks != null
                ? new List<PeriodContentBlockRuntime>(periodContentBlocks)
                : Array.Empty<PeriodContentBlockRuntime>();
            PostPeriodSurveyRefs = postPeriodSurveyRefs != null
                ? new List<SurveyRefRuntime>(postPeriodSurveyRefs)
                : Array.Empty<SurveyRefRuntime>();
            InterPeriodBlocks = interPeriodBlocks != null
                ? new List<InterPeriodBlockRuntime>(interPeriodBlocks)
                : Array.Empty<InterPeriodBlockRuntime>();
            InternalCode = internalCode ?? string.Empty;
            RawNode = rawNode ?? JsonValue.Null;
        }

        public bool HasFeature(string featureName)
        {
            if (EnabledFeatures == null || EnabledFeatures.Count == 0 || string.IsNullOrWhiteSpace(featureName))
            {
                return false;
            }

            for (var index = 0; index < EnabledFeatures.Count; index++)
            {
                if (string.Equals(EnabledFeatures[index], featureName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }

    public sealed class SessionConfigRuntime
    {
        public static SessionConfigRuntime Empty { get; } = new SessionConfigRuntime(
            0,
            GlobalSettingsRuntime.Empty,
            SessionFlowRuntime.Empty,
            SessionFlowRuntime.Empty,
            Array.Empty<SharedSurveyRefRuntime>(),
            Array.Empty<SessionPeriodRuntime>(),
            JsonValue.Null,
            false,
            string.Empty);

        public int SchemaVersion { get; }
        public GlobalSettingsRuntime GlobalSettings { get; }
        public SessionFlowRuntime PreSessionFlow { get; }
        public SessionFlowRuntime PostSessionFlow { get; }
        public IReadOnlyList<SharedSurveyRefRuntime> SharedSurveyRefs { get; }
        public IReadOnlyList<SessionPeriodRuntime> Periods { get; }
        public JsonValue RootNode { get; }
        public bool IsValid { get; }
        public string ValidationError { get; }

        public SessionConfigRuntime(
            int schemaVersion,
            GlobalSettingsRuntime globalSettings,
            SessionFlowRuntime preSessionFlow,
            SessionFlowRuntime postSessionFlow,
            IReadOnlyList<SharedSurveyRefRuntime> sharedSurveyRefs,
            IReadOnlyList<SessionPeriodRuntime> periods,
            JsonValue rootNode,
            bool isValid,
            string validationError)
        {
            SchemaVersion = schemaVersion;
            GlobalSettings = globalSettings ?? GlobalSettingsRuntime.Empty;
            PreSessionFlow = preSessionFlow ?? SessionFlowRuntime.Empty;
            PostSessionFlow = postSessionFlow ?? SessionFlowRuntime.Empty;
            SharedSurveyRefs = sharedSurveyRefs != null
                ? new List<SharedSurveyRefRuntime>(sharedSurveyRefs)
                : Array.Empty<SharedSurveyRefRuntime>();
            Periods = periods != null
                ? new List<SessionPeriodRuntime>(periods)
                : Array.Empty<SessionPeriodRuntime>();
            RootNode = rootNode ?? JsonValue.Null;
            IsValid = isValid;
            ValidationError = validationError ?? string.Empty;
        }

        public bool TryGetPeriod(int periodNumber, out SessionPeriodRuntime period)
        {
            if (Periods != null)
            {
                for (var index = 0; index < Periods.Count; index++)
                {
                    var candidate = Periods[index];

                    if (candidate != null && candidate.Number == periodNumber)
                    {
                        period = candidate;
                        return true;
                    }
                }
            }

            period = SessionPeriodRuntime.Empty;
            return false;
        }

        public bool TryGetSharedSurveyRef(string sharedRefId, out SharedSurveyRefRuntime surveyRef)
        {
            if (!string.IsNullOrWhiteSpace(sharedRefId) && SharedSurveyRefs != null)
            {
                for (var index = 0; index < SharedSurveyRefs.Count; index++)
                {
                    var candidate = SharedSurveyRefs[index];

                    if (candidate != null
                        && string.Equals(candidate.Id, sharedRefId, StringComparison.OrdinalIgnoreCase))
                    {
                        surveyRef = candidate;
                        return true;
                    }
                }
            }

            surveyRef = SharedSurveyRefRuntime.Empty;
            return false;
        }
    }

    public sealed class SessionFlowStepDescriptor
    {
        public static SessionFlowStepDescriptor Empty { get; } = new SessionFlowStepDescriptor(
            string.Empty,
            SessionFlowStepScope.None,
            0,
            SessionFlowStepType.Unknown,
            string.Empty,
            false);

        public string Key { get; }
        public SessionFlowStepScope Scope { get; }
        public int PeriodNumber { get; }
        public SessionFlowStepType Type { get; }
        public string Title { get; }
        public bool IsRequired { get; }
        public bool IsDefined => !string.IsNullOrWhiteSpace(Key) && Scope != SessionFlowStepScope.None;

        public SessionFlowStepDescriptor(
            string key,
            SessionFlowStepScope scope,
            int periodNumber,
            SessionFlowStepType type,
            string title,
            bool isRequired)
        {
            Key = key ?? string.Empty;
            Scope = scope;
            PeriodNumber = periodNumber;
            Type = type;
            Title = title ?? string.Empty;
            IsRequired = isRequired;
        }
    }

    public sealed class SessionFlowProgressState
    {
        public static SessionFlowProgressState Empty { get; } = new SessionFlowProgressState(
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<int>(),
            0,
            SessionFlowStepDescriptor.Empty,
            false,
            false);

        public IReadOnlyList<string> CompletedPreSessionStepKeys { get; }
        public IReadOnlyList<string> CompletedPeriodContentBlockKeys { get; }
        public IReadOnlyList<string> CompletedPostPeriodSurveyKeys { get; }
        public IReadOnlyList<string> CompletedInterPeriodBlockKeys { get; }
        public IReadOnlyList<string> CompletedPostSessionStepKeys { get; }
        public IReadOnlyList<int> CompletedPeriodNumbers { get; }
        public int ActivePeriodNumber { get; }
        public SessionFlowStepDescriptor ActiveStep { get; }
        public bool IsPostSessionCompleted { get; }
        public bool IsSessionCompleted { get; }

        public SessionFlowProgressState(
            IReadOnlyList<string> completedPreSessionStepKeys,
            IReadOnlyList<string> completedPeriodContentBlockKeys,
            IReadOnlyList<string> completedPostPeriodSurveyKeys,
            IReadOnlyList<string> completedInterPeriodBlockKeys,
            IReadOnlyList<string> completedPostSessionStepKeys,
            IReadOnlyList<int> completedPeriodNumbers,
            int activePeriodNumber,
            SessionFlowStepDescriptor activeStep,
            bool isPostSessionCompleted,
            bool isSessionCompleted)
        {
            CompletedPreSessionStepKeys = completedPreSessionStepKeys != null
                ? new List<string>(completedPreSessionStepKeys)
                : Array.Empty<string>();
            CompletedPeriodContentBlockKeys = completedPeriodContentBlockKeys != null
                ? new List<string>(completedPeriodContentBlockKeys)
                : Array.Empty<string>();
            CompletedPostPeriodSurveyKeys = completedPostPeriodSurveyKeys != null
                ? new List<string>(completedPostPeriodSurveyKeys)
                : Array.Empty<string>();
            CompletedInterPeriodBlockKeys = completedInterPeriodBlockKeys != null
                ? new List<string>(completedInterPeriodBlockKeys)
                : Array.Empty<string>();
            CompletedPostSessionStepKeys = completedPostSessionStepKeys != null
                ? new List<string>(completedPostSessionStepKeys)
                : Array.Empty<string>();
            CompletedPeriodNumbers = completedPeriodNumbers != null
                ? new List<int>(completedPeriodNumbers)
                : Array.Empty<int>();
            ActivePeriodNumber = activePeriodNumber;
            ActiveStep = activeStep ?? SessionFlowStepDescriptor.Empty;
            IsPostSessionCompleted = isPostSessionCompleted;
            IsSessionCompleted = isSessionCompleted;
        }

        public bool IsCompleted(SessionFlowStepScope scope, string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            switch (scope)
            {
                case SessionFlowStepScope.PreSession:
                    return Contains(CompletedPreSessionStepKeys, key);
                case SessionFlowStepScope.PeriodContent:
                    return Contains(CompletedPeriodContentBlockKeys, key);
                case SessionFlowStepScope.PostPeriodSurvey:
                    return Contains(CompletedPostPeriodSurveyKeys, key);
                case SessionFlowStepScope.InterPeriodBlock:
                    return Contains(CompletedInterPeriodBlockKeys, key);
                case SessionFlowStepScope.PostSession:
                    return Contains(CompletedPostSessionStepKeys, key);
                default:
                    return false;
            }
        }

        public bool IsPeriodCompleted(int periodNumber)
        {
            if (periodNumber <= 0)
            {
                return false;
            }

            for (var index = 0; index < CompletedPeriodNumbers.Count; index++)
            {
                if (CompletedPeriodNumbers[index] == periodNumber)
                {
                    return true;
                }
            }

            return false;
        }

        public SessionFlowProgressState WithActiveStep(SessionFlowStepDescriptor activeStep, int activePeriodNumber)
        {
            return new SessionFlowProgressState(
                CompletedPreSessionStepKeys,
                CompletedPeriodContentBlockKeys,
                CompletedPostPeriodSurveyKeys,
                CompletedInterPeriodBlockKeys,
                CompletedPostSessionStepKeys,
                CompletedPeriodNumbers,
                activePeriodNumber,
                activeStep,
                IsPostSessionCompleted,
                IsSessionCompleted);
        }

        public SessionFlowProgressState WithCompleted(SessionFlowStepScope scope, string key)
        {
            switch (scope)
            {
                case SessionFlowStepScope.PreSession:
                    return new SessionFlowProgressState(
                        AddUnique(CompletedPreSessionStepKeys, key),
                        CompletedPeriodContentBlockKeys,
                        CompletedPostPeriodSurveyKeys,
                        CompletedInterPeriodBlockKeys,
                        CompletedPostSessionStepKeys,
                        CompletedPeriodNumbers,
                        ActivePeriodNumber,
                        ActiveStep,
                        IsPostSessionCompleted,
                        IsSessionCompleted);
                case SessionFlowStepScope.PeriodContent:
                    return new SessionFlowProgressState(
                        CompletedPreSessionStepKeys,
                        AddUnique(CompletedPeriodContentBlockKeys, key),
                        CompletedPostPeriodSurveyKeys,
                        CompletedInterPeriodBlockKeys,
                        CompletedPostSessionStepKeys,
                        CompletedPeriodNumbers,
                        ActivePeriodNumber,
                        ActiveStep,
                        IsPostSessionCompleted,
                        IsSessionCompleted);
                case SessionFlowStepScope.PostPeriodSurvey:
                    return new SessionFlowProgressState(
                        CompletedPreSessionStepKeys,
                        CompletedPeriodContentBlockKeys,
                        AddUnique(CompletedPostPeriodSurveyKeys, key),
                        CompletedInterPeriodBlockKeys,
                        CompletedPostSessionStepKeys,
                        CompletedPeriodNumbers,
                        ActivePeriodNumber,
                        ActiveStep,
                        IsPostSessionCompleted,
                        IsSessionCompleted);
                case SessionFlowStepScope.InterPeriodBlock:
                    return new SessionFlowProgressState(
                        CompletedPreSessionStepKeys,
                        CompletedPeriodContentBlockKeys,
                        CompletedPostPeriodSurveyKeys,
                        AddUnique(CompletedInterPeriodBlockKeys, key),
                        CompletedPostSessionStepKeys,
                        CompletedPeriodNumbers,
                        ActivePeriodNumber,
                        ActiveStep,
                        IsPostSessionCompleted,
                        IsSessionCompleted);
                case SessionFlowStepScope.PostSession:
                    return new SessionFlowProgressState(
                        CompletedPreSessionStepKeys,
                        CompletedPeriodContentBlockKeys,
                        CompletedPostPeriodSurveyKeys,
                        CompletedInterPeriodBlockKeys,
                        AddUnique(CompletedPostSessionStepKeys, key),
                        CompletedPeriodNumbers,
                        ActivePeriodNumber,
                        ActiveStep,
                        IsPostSessionCompleted,
                        IsSessionCompleted);
                default:
                    return this;
            }
        }

        public SessionFlowProgressState WithCompletedPeriod(int periodNumber)
        {
            return new SessionFlowProgressState(
                CompletedPreSessionStepKeys,
                CompletedPeriodContentBlockKeys,
                CompletedPostPeriodSurveyKeys,
                CompletedInterPeriodBlockKeys,
                CompletedPostSessionStepKeys,
                AddUniqueNumber(CompletedPeriodNumbers, periodNumber),
                ActivePeriodNumber,
                ActiveStep,
                IsPostSessionCompleted,
                IsSessionCompleted);
        }

        public SessionFlowProgressState WithPostSessionCompleted(bool value)
        {
            return new SessionFlowProgressState(
                CompletedPreSessionStepKeys,
                CompletedPeriodContentBlockKeys,
                CompletedPostPeriodSurveyKeys,
                CompletedInterPeriodBlockKeys,
                CompletedPostSessionStepKeys,
                CompletedPeriodNumbers,
                ActivePeriodNumber,
                ActiveStep,
                value,
                IsSessionCompleted);
        }

        public SessionFlowProgressState WithSessionCompleted(bool value)
        {
            return new SessionFlowProgressState(
                CompletedPreSessionStepKeys,
                CompletedPeriodContentBlockKeys,
                CompletedPostPeriodSurveyKeys,
                CompletedInterPeriodBlockKeys,
                CompletedPostSessionStepKeys,
                CompletedPeriodNumbers,
                ActivePeriodNumber,
                ActiveStep,
                IsPostSessionCompleted,
                value);
        }

        private static bool Contains(IReadOnlyList<string> source, string value)
        {
            if (source == null || string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            for (var index = 0; index < source.Count; index++)
            {
                if (string.Equals(source[index], value, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static IReadOnlyList<string> AddUnique(IReadOnlyList<string> source, string value)
        {
            var result = source != null
                ? new List<string>(source)
                : new List<string>();

            if (string.IsNullOrWhiteSpace(value))
            {
                return result;
            }

            for (var index = 0; index < result.Count; index++)
            {
                if (string.Equals(result[index], value, StringComparison.Ordinal))
                {
                    return result;
                }
            }

            result.Add(value);
            return result;
        }

        private static IReadOnlyList<int> AddUniqueNumber(IReadOnlyList<int> source, int value)
        {
            var result = source != null
                ? new List<int>(source)
                : new List<int>();

            if (value <= 0)
            {
                return result;
            }

            for (var index = 0; index < result.Count; index++)
            {
                if (result[index] == value)
                {
                    return result;
                }
            }

            result.Add(value);
            return result;
        }
    }

    public enum SessionFlowQuestionType
    {
        Text,
        SingleChoice,
        MultipleChoice,
        Scale
    }

    public sealed class SessionFlowQuestionOptionRuntime
    {
        public string Id { get; }
        public string Label { get; }
        public bool IsOther { get; }
        public bool IsCorrect { get; }

        public SessionFlowQuestionOptionRuntime(
            string id,
            string label,
            bool isOther,
            bool isCorrect)
        {
            Id = id ?? string.Empty;
            Label = label ?? string.Empty;
            IsOther = isOther;
            IsCorrect = isCorrect;
        }
    }

    public sealed class SessionFlowQuestionRuntime
    {
        public string Id { get; }
        public string Label { get; }
        public string Placeholder { get; }
        public string Description { get; }
        public bool IsRequired { get; }
        public SessionFlowQuestionType Type { get; }
        public IReadOnlyList<SessionFlowQuestionOptionRuntime> Options { get; } 
        public int PageIndex { get; }
        public string PageCode { get; }
        public bool IsCheckableChoiceQuestion
        {
            get
            {
                if (Type != SessionFlowQuestionType.SingleChoice
                    && Type != SessionFlowQuestionType.MultipleChoice)
                {
                    return false;
                }

                if (Options == null)
                {
                    return false;
                }

                for (var index = 0; index < Options.Count; index++)
                {
                    if (Options[index] != null && Options[index].IsCorrect)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public SessionFlowQuestionRuntime(
            string id,
            string label,
            string placeholder,
            string description,
            bool isRequired,
            SessionFlowQuestionType type,
            IReadOnlyList<SessionFlowQuestionOptionRuntime> options,
            int pageIndex,
            string pageCode)
        {
            Id = id ?? string.Empty;
            Label = label ?? string.Empty;
            Placeholder = placeholder ?? string.Empty;
            Description = description ?? string.Empty;
            IsRequired = isRequired;
            Type = type;
            Options = options != null
                ? new List<SessionFlowQuestionOptionRuntime>(options)
                : Array.Empty<SessionFlowQuestionOptionRuntime>();
            PageIndex = pageIndex < 0 ? 0 : pageIndex;
            PageCode = pageCode ?? string.Empty;
        }

        public SessionFlowQuestionRuntime(
            string id,
            string label,
            string placeholder,
            string description,
            bool isRequired,
            SessionFlowQuestionType type,
            IReadOnlyList<SessionFlowQuestionOptionRuntime> options)
            : this(
                id,
                label,
                placeholder,
                description,
                isRequired,
                type,
                options,
                0,
                string.Empty)
        {
        }
    }

    public sealed class SessionFlowStepViewModel
    {
        public static SessionFlowStepViewModel Empty { get; } = new SessionFlowStepViewModel(
            SessionFlowStepDescriptor.Empty,
            SessionFlowRendererKind.None,
            string.Empty,
            string.Empty,
            string.Empty,
            false,
            false,
            string.Empty,
            string.Empty,
            string.Empty,
            Array.Empty<SessionFlowQuestionRuntime>());

        public SessionFlowStepDescriptor Descriptor { get; }
        public SessionFlowRendererKind RendererKind { get; }
        public string Title { get; }
        public string Subtitle { get; }
        public string Body { get; }
        public bool IsRequired { get; }
        public bool CanSkip { get; }
        public string PrimaryActionText { get; }
        public string SecondaryActionText { get; }
        public string ErrorMessage { get; }
        public IReadOnlyList<SessionFlowQuestionRuntime> Questions { get; }

        public SessionFlowStepViewModel(
            SessionFlowStepDescriptor descriptor,
            SessionFlowRendererKind rendererKind,
            string title,
            string subtitle,
            string body,
            bool isRequired,
            bool canSkip,
            string primaryActionText,
            string secondaryActionText,
            string errorMessage,
            IReadOnlyList<SessionFlowQuestionRuntime> questions)
        {
            Descriptor = descriptor ?? SessionFlowStepDescriptor.Empty;
            RendererKind = rendererKind;
            Title = title ?? string.Empty;
            Subtitle = subtitle ?? string.Empty;
            Body = body ?? string.Empty;
            IsRequired = isRequired;
            CanSkip = canSkip;
            PrimaryActionText = primaryActionText ?? string.Empty;
            SecondaryActionText = secondaryActionText ?? string.Empty;
            ErrorMessage = errorMessage ?? string.Empty;
            Questions = questions != null
                ? new List<SessionFlowQuestionRuntime>(questions)
                : Array.Empty<SessionFlowQuestionRuntime>();
        }
    }

    public sealed class SessionFlowRuntimeState
    {
        public static SessionFlowRuntimeState Empty { get; } = new SessionFlowRuntimeState(
            SessionConfigRuntime.Empty,
            SessionFlowProgressState.Empty,
            SessionFlowStepViewModel.Empty,
            string.Empty,
            string.Empty,
            false);

        public SessionConfigRuntime Config { get; }
        public SessionFlowProgressState Progress { get; }
        public SessionFlowStepViewModel ActiveStepView { get; }
        public string StatusMessage { get; }
        public string LastError { get; }
        public bool IsCompleted { get; }

        public SessionFlowRuntimeState(
            SessionConfigRuntime config,
            SessionFlowProgressState progress,
            SessionFlowStepViewModel activeStepView,
            string statusMessage,
            string lastError,
            bool isCompleted)
        {
            Config = config ?? SessionConfigRuntime.Empty;
            Progress = progress ?? SessionFlowProgressState.Empty;
            ActiveStepView = activeStepView ?? SessionFlowStepViewModel.Empty;
            StatusMessage = statusMessage ?? string.Empty;
            LastError = lastError ?? string.Empty;
            IsCompleted = isCompleted;
        }
    }
}
