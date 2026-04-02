using System;
using System.Collections.Generic;

namespace Game.Core.Application.Session
{
    public enum RunLifecycleStatus
    {
        Unknown,
        New,
        InProgress,
        Completed,
        Aborted
    }

    public enum SessionDefinitionStatus
    {
        Unknown,
        Draft,
        Active,
        Archived
    }

    public enum SurveyTemplateKind
    {
        Unknown,
        Pre,
        Periodic,
        Post
    }

    public sealed class AuthTokenData
    {
        public static AuthTokenData Empty { get; } = new AuthTokenData(string.Empty, string.Empty);

        public string Token { get; }
        public string SavedAtUtc { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Token);

        public AuthTokenData(string token, string savedAtUtc)
        {
            Token = token ?? string.Empty;
            SavedAtUtc = savedAtUtc ?? string.Empty;
        }
    }

    public sealed class AuthenticatedRunInfo
    {
        public static AuthenticatedRunInfo Empty { get; } = new AuthenticatedRunInfo(
            string.Empty,
            string.Empty,
            RunLifecycleStatus.Unknown,
            0,
            string.Empty);

        public string RunId { get; }
        public string SessionDefinitionCode { get; }
        public RunLifecycleStatus RunStatus { get; }
        public int CurrentPeriodNumber { get; }
        public string AssignedGroupCode { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(RunId);

        public AuthenticatedRunInfo(
            string runId,
            string sessionDefinitionCode,
            RunLifecycleStatus runStatus,
            int currentPeriodNumber,
            string assignedGroupCode)
        {
            RunId = runId ?? string.Empty;
            SessionDefinitionCode = sessionDefinitionCode ?? string.Empty;
            RunStatus = runStatus;
            CurrentPeriodNumber = currentPeriodNumber;
            AssignedGroupCode = assignedGroupCode ?? string.Empty;
        }
    }

    public sealed class BootstrapRunRuntimeModel
    {
        public string RunId { get; }
        public RunLifecycleStatus RunStatus { get; }
        public int CurrentPeriodNumber { get; }
        public int BootstrapVersion { get; }
        public string StartedAtRaw { get; }
        public string FinishedAtRaw { get; }
        public string LastCheckpointAtRaw { get; }

        public BootstrapRunRuntimeModel(
            string runId,
            RunLifecycleStatus runStatus,
            int currentPeriodNumber,
            int bootstrapVersion,
            string startedAtRaw,
            string finishedAtRaw,
            string lastCheckpointAtRaw)
        {
            RunId = runId ?? string.Empty;
            RunStatus = runStatus;
            CurrentPeriodNumber = currentPeriodNumber;
            BootstrapVersion = bootstrapVersion;
            StartedAtRaw = startedAtRaw ?? string.Empty;
            FinishedAtRaw = finishedAtRaw ?? string.Empty;
            LastCheckpointAtRaw = lastCheckpointAtRaw ?? string.Empty;
        }
    }

    public sealed class SessionRuntimeModel
    {
        public int SessionDefinitionId { get; }
        public string Code { get; }
        public string Title { get; }
        public string Description { get; }
        public SessionDefinitionStatus Status { get; }
        public int ConfigVersion { get; }
        public int ParticipantCountPlanned { get; }
        public ParsedSessionConfigModel SessionConfig { get; }

        public SessionRuntimeModel(
            int sessionDefinitionId,
            string code,
            string title,
            string description,
            SessionDefinitionStatus status,
            int configVersion,
            int participantCountPlanned,
            ParsedSessionConfigModel sessionConfig)
        {
            SessionDefinitionId = sessionDefinitionId;
            Code = code ?? string.Empty;
            Title = title ?? string.Empty;
            Description = description ?? string.Empty;
            Status = status;
            ConfigVersion = configVersion;
            ParticipantCountPlanned = participantCountPlanned;
            SessionConfig = sessionConfig ?? ParsedSessionConfigModel.Empty;
        }
    }

    public sealed class ParticipantRuntimeModel
    {
        public int ParticipantAccountId { get; }
        public string Login { get; }
        public string AssignedGroupCode { get; }
        public ParsedAssignedConfigModel AssignedConfig { get; }
        public ParsedJsonDocument DeviceBinding { get; }

        public ParticipantRuntimeModel(
            int participantAccountId,
            string login,
            string assignedGroupCode,
            ParsedAssignedConfigModel assignedConfig,
            ParsedJsonDocument deviceBinding)
        {
            ParticipantAccountId = participantAccountId;
            Login = login ?? string.Empty;
            AssignedGroupCode = assignedGroupCode ?? string.Empty;
            AssignedConfig = assignedConfig ?? ParsedAssignedConfigModel.Empty;
            DeviceBinding = deviceBinding ?? ParsedJsonDocument.Empty;
        }
    }

    public sealed class SurveyTemplateRuntimeModel
    {
        public int Id { get; }
        public string Code { get; }
        public string Title { get; }
        public SurveyTemplateKind Type { get; }
        public int Version { get; }
        public ParsedJsonDocument TemplateDocument { get; }

        public SurveyTemplateRuntimeModel(
            int id,
            string code,
            string title,
            SurveyTemplateKind type,
            int version,
            ParsedJsonDocument templateDocument)
        {
            Id = id;
            Code = code ?? string.Empty;
            Title = title ?? string.Empty;
            Type = type;
            Version = version;
            TemplateDocument = templateDocument ?? ParsedJsonDocument.Empty;
        }
    }

    public sealed class PeriodStatisticsRuntime
    {
        public static PeriodStatisticsRuntime Empty { get; } = new PeriodStatisticsRuntime(
            0,
            string.Empty,
            null,
            null,
            null,
            null,
            null);

        public int PeriodNumber { get; }
        public string HistoricalYear { get; }
        public double? NominalIncomeGrowth { get; }
        public double? Inflation { get; }
        public double? DepositRate { get; }
        public double? CreditRate { get; }
        public double? MortgageRate { get; }
        public bool HasValues =>
            !string.IsNullOrWhiteSpace(HistoricalYear)
            || NominalIncomeGrowth.HasValue
            || Inflation.HasValue
            || DepositRate.HasValue
            || CreditRate.HasValue
            || MortgageRate.HasValue;

        public PeriodStatisticsRuntime(
            int periodNumber,
            string historicalYear,
            double? nominalIncomeGrowth,
            double? inflation,
            double? depositRate,
            double? creditRate,
            double? mortgageRate)
        {
            PeriodNumber = periodNumber;
            HistoricalYear = historicalYear ?? string.Empty;
            NominalIncomeGrowth = nominalIncomeGrowth;
            Inflation = inflation;
            DepositRate = depositRate;
            CreditRate = creditRate;
            MortgageRate = mortgageRate;
        }
    }

    public sealed class StatisticalDatasetRuntimeModel
    {
        public static StatisticalDatasetRuntimeModel Empty { get; } = new StatisticalDatasetRuntimeModel(
            0,
            string.Empty,
            string.Empty,
            0,
            ParsedJsonDocument.Empty,
            new Dictionary<int, PeriodStatisticsRuntime>());

        public int Id { get; }
        public string Code { get; }
        public string Title { get; }
        public int Version { get; }
        public ParsedJsonDocument DatasetDocument { get; }
        public IReadOnlyDictionary<int, PeriodStatisticsRuntime> PeriodStatisticsByPeriod { get; }
        public bool HasDataset => Id > 0 || !string.IsNullOrWhiteSpace(Code) || PeriodStatisticsByPeriod.Count > 0;

        public StatisticalDatasetRuntimeModel(
            int id,
            string code,
            string title,
            int version,
            ParsedJsonDocument datasetDocument,
            IReadOnlyDictionary<int, PeriodStatisticsRuntime> periodStatisticsByPeriod)
        {
            Id = id;
            Code = code ?? string.Empty;
            Title = title ?? string.Empty;
            Version = version;
            DatasetDocument = datasetDocument ?? ParsedJsonDocument.Empty;
            PeriodStatisticsByPeriod = periodStatisticsByPeriod != null
                ? new Dictionary<int, PeriodStatisticsRuntime>(periodStatisticsByPeriod)
                : new Dictionary<int, PeriodStatisticsRuntime>();
        }

        public bool TryGetPeriodStatistics(int periodNumber, out PeriodStatisticsRuntime periodStatistics)
        {
            if (periodNumber > 0
                && PeriodStatisticsByPeriod != null
                && PeriodStatisticsByPeriod.TryGetValue(periodNumber, out periodStatistics)
                && periodStatistics != null)
            {
                return true;
            }

            periodStatistics = PeriodStatisticsRuntime.Empty;
            return false;
        }
    }

    public sealed class BootstrapPayload
    {
        public BootstrapRunRuntimeModel Run { get; }
        public SessionRuntimeModel Session { get; }
        public ParticipantRuntimeModel Participant { get; }
        public IReadOnlyList<SurveyTemplateRuntimeModel> SurveyTemplates { get; }
        public StatisticalDatasetRuntimeModel StatisticalDataset { get; }
        public int BootstrapVersion => Run?.BootstrapVersion ?? 0;

        public BootstrapPayload(
            BootstrapRunRuntimeModel run,
            SessionRuntimeModel session,
            ParticipantRuntimeModel participant,
            IReadOnlyList<SurveyTemplateRuntimeModel> surveyTemplates,
            StatisticalDatasetRuntimeModel statisticalDataset)
        {
            Run = run;
            Session = session;
            Participant = participant;
            SurveyTemplates = surveyTemplates ?? Array.Empty<SurveyTemplateRuntimeModel>();
            StatisticalDataset = statisticalDataset ?? StatisticalDatasetRuntimeModel.Empty;
        }
    }

    public sealed class ParsedJsonDocument
    {
        public static ParsedJsonDocument Empty { get; } =
            new ParsedJsonDocument(string.Empty, JsonValue.Null, true, true, string.Empty);

        public string RawJson { get; }
        public JsonValue Root { get; }
        public bool IsEmpty { get; }
        public bool IsValid { get; }
        public string ParseError { get; }
        public string Summary
        {
            get
            {
                if (IsEmpty)
                {
                    return "empty";
                }

                if (!IsValid)
                {
                    return "invalid";
                }

                switch (Root.Kind)
                {
                    case JsonValueKind.Object:
                        return $"object, {Root.Count} keys";
                    case JsonValueKind.Array:
                        return $"array, {Root.Count} items";
                    case JsonValueKind.String:
                        return "string";
                    case JsonValueKind.Number:
                        return "number";
                    case JsonValueKind.Boolean:
                        return "boolean";
                    default:
                        return "null";
                }
            }
        }

        public ParsedJsonDocument(
            string rawJson,
            JsonValue root,
            bool isEmpty,
            bool isValid,
            string parseError)
        {
            RawJson = rawJson ?? string.Empty;
            Root = root ?? JsonValue.Null;
            IsEmpty = isEmpty;
            IsValid = isValid;
            ParseError = parseError ?? string.Empty;
        }
    }

    public sealed class ParsedSessionConfigModel
    {
        public static ParsedSessionConfigModel Empty { get; } =
            new ParsedSessionConfigModel(ParsedJsonDocument.Empty);

        public ParsedJsonDocument Document { get; }
        public string Summary => Document.Summary;
        public int? PeriodCount
        {
            get
            {
                if (!Document.IsValid || Document.Root.Kind != JsonValueKind.Object)
                {
                    return null;
                }

                if (Document.Root.TryGetProperty("periods", out var periods)
                    && periods.Kind == JsonValueKind.Array)
                {
                    return periods.Count;
                }

                if (Document.Root.TryGetProperty("periodDefinitions", out var definitions)
                    && definitions.Kind == JsonValueKind.Array)
                {
                    return definitions.Count;
                }

                if (Document.Root.TryGetProperty("periodConfigs", out var configs)
                    && configs.Kind == JsonValueKind.Array)
                {
                    return configs.Count;
                }

                var descendantCount = FindDescendantArrayCount(Document.Root, "periods", "periodDefinitions", "periodConfigs");

                if (descendantCount.HasValue)
                {
                    return descendantCount.Value;
                }

                return null;
            }
        }

        public ParsedSessionConfigModel(ParsedJsonDocument document)
        {
            Document = document ?? ParsedJsonDocument.Empty;
        }

        private static int? FindDescendantArrayCount(JsonValue node, params string[] propertyNames)
        {
            if (node == null || propertyNames == null || propertyNames.Length == 0)
            {
                return null;
            }

            if (node.Kind == JsonValueKind.Object)
            {
                foreach (var propertyName in propertyNames)
                {
                    if (!string.IsNullOrWhiteSpace(propertyName)
                        && node.TryGetProperty(propertyName, out var value)
                        && value != null
                        && value.Kind == JsonValueKind.Array)
                    {
                        return value.Count;
                    }
                }

                foreach (var pair in node.ObjectValue)
                {
                    var nestedCount = FindDescendantArrayCount(pair.Value, propertyNames);

                    if (nestedCount.HasValue)
                    {
                        return nestedCount.Value;
                    }
                }
            }

            if (node.Kind == JsonValueKind.Array)
            {
                foreach (var item in node.ArrayValue)
                {
                    var nestedCount = FindDescendantArrayCount(item, propertyNames);

                    if (nestedCount.HasValue)
                    {
                        return nestedCount.Value;
                    }
                }
            }

            return null;
        }
    }

    public sealed class ParsedAssignedConfigModel
    {
        public static ParsedAssignedConfigModel Empty { get; } =
            new ParsedAssignedConfigModel(ParsedJsonDocument.Empty);

        public ParsedJsonDocument Document { get; }
        public string Summary => Document.Summary;

        public ParsedAssignedConfigModel(ParsedJsonDocument document)
        {
            Document = document ?? ParsedJsonDocument.Empty;
        }
    }

    public sealed class ClientRuntimeState
    {
        public static ClientRuntimeState Empty { get; } = new ClientRuntimeState(
            AuthTokenData.Empty,
            AuthenticatedRunInfo.Empty,
            null,
            false);

        public AuthTokenData AuthToken { get; }
        public AuthenticatedRunInfo AuthenticatedRun { get; }
        public BootstrapPayload Bootstrap { get; }
        public bool CanRestore { get; }
        public bool HasSession =>
            AuthToken != null && AuthToken.IsValid
            && AuthenticatedRun != null && AuthenticatedRun.IsValid
            && Bootstrap != null;

        public ClientRuntimeState(
            AuthTokenData authToken,
            AuthenticatedRunInfo authenticatedRun,
            BootstrapPayload bootstrap,
            bool canRestore)
        {
            AuthToken = authToken ?? AuthTokenData.Empty;
            AuthenticatedRun = authenticatedRun ?? AuthenticatedRunInfo.Empty;
            Bootstrap = bootstrap;
            CanRestore = canRestore;
        }
    }

    public static class SessionContractMapper
    {
        public static AuthenticatedRunInfo ToAuthenticatedRunInfo(RunInfoDto dto)
        {
            if (dto == null)
            {
                return AuthenticatedRunInfo.Empty;
            }

            return new AuthenticatedRunInfo(
                dto.runId,
                dto.sessionDefinitionCode,
                ToRunStatus(dto.runStatus),
                dto.currentPeriodNumber,
                dto.assignedGroupCode);
        }

        public static RunLifecycleStatus ToRunStatus(string rawValue)
        {
            switch ((rawValue ?? string.Empty).Trim().ToUpperInvariant())
            {
                case "NEW":
                    return RunLifecycleStatus.New;
                case "IN_PROGRESS":
                    return RunLifecycleStatus.InProgress;
                case "COMPLETED":
                    return RunLifecycleStatus.Completed;
                case "ABORTED":
                    return RunLifecycleStatus.Aborted;
                default:
                    return RunLifecycleStatus.Unknown;
            }
        }

        public static string ToRunStatusCode(RunLifecycleStatus value)
        {
            switch (value)
            {
                case RunLifecycleStatus.New:
                    return "NEW";
                case RunLifecycleStatus.InProgress:
                    return "IN_PROGRESS";
                case RunLifecycleStatus.Completed:
                    return "COMPLETED";
                case RunLifecycleStatus.Aborted:
                    return "ABORTED";
                default:
                    return "UNKNOWN";
            }
        }

        public static SessionDefinitionStatus ToSessionStatus(string rawValue)
        {
            switch ((rawValue ?? string.Empty).Trim().ToUpperInvariant())
            {
                case "DRAFT":
                    return SessionDefinitionStatus.Draft;
                case "ACTIVE":
                    return SessionDefinitionStatus.Active;
                case "ARCHIVED":
                    return SessionDefinitionStatus.Archived;
                default:
                    return SessionDefinitionStatus.Unknown;
            }
        }

        public static SurveyTemplateKind ToSurveyTemplateKind(string rawValue)
        {
            switch ((rawValue ?? string.Empty).Trim().ToUpperInvariant())
            {
                case "PRE":
                    return SurveyTemplateKind.Pre;
                case "PERIODIC":
                    return SurveyTemplateKind.Periodic;
                case "POST":
                    return SurveyTemplateKind.Post;
                default:
                    return SurveyTemplateKind.Unknown;
            }
        }
    }
}
