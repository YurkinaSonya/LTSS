using System;
using System.Collections.Generic;
using Game.Core.Application.Logging;
using Game.Core.Application.Networking;

namespace Game.Core.Application.Session
{
    public sealed class SessionRuntimeFactory : ISessionRuntimeFactory
    {
        private readonly IJsonSerializer _serializer;
        private readonly IJsonNodeParser _jsonNodeParser;
        private readonly IAppLogger _logger;

        public SessionRuntimeFactory(
            IJsonSerializer serializer,
            IJsonNodeParser jsonNodeParser,
            IAppLogger logger)
        {
            _serializer = serializer;
            _jsonNodeParser = jsonNodeParser;
            _logger = logger;
        }

        public bool TryBuild(
            AuthTokenData tokenData,
            AuthenticatedRunInfo runInfo,
            BootstrapResponseDto response,
            out ClientRuntimeState runtimeState,
            out string error)
        {
            runtimeState = ClientRuntimeState.Empty;
            error = string.Empty;

            if (tokenData == null || !tokenData.IsValid)
            {
                error = "Auth token is missing.";
                return false;
            }

            if (response == null)
            {
                error = "Bootstrap response payload is missing.";
                return false;
            }

            var effectiveRunId = response.run != null && !string.IsNullOrWhiteSpace(response.run.runId)
                ? response.run.runId
                : runInfo?.RunId;

            if (string.IsNullOrWhiteSpace(effectiveRunId))
            {
                error = "Bootstrap response does not contain a valid run id.";
                return false;
            }

            var responseRunStatus = SessionContractMapper.ToRunStatus(response.run?.runStatus);
            var effectiveCurrentPeriodNumber = Math.Max(
                response.run?.currentPeriodNumber ?? 0,
                runInfo?.CurrentPeriodNumber ?? 0);
            var effectiveRunStatus = runInfo != null && runInfo.RunStatus != RunLifecycleStatus.Unknown
                ? runInfo.RunStatus
                : responseRunStatus;

            if (responseRunStatus == RunLifecycleStatus.Completed
                || responseRunStatus == RunLifecycleStatus.Aborted)
            {
                effectiveRunStatus = responseRunStatus;
            }

            var bootstrapRun = new BootstrapRunRuntimeModel(
                effectiveRunId,
                effectiveRunStatus,
                effectiveCurrentPeriodNumber,
                response.run?.bootstrapVersion ?? 0,
                response.run?.startedAt,
                response.run?.finishedAt,
                response.run?.lastCheckpointAt);

            var effectiveRunInfo = new AuthenticatedRunInfo(
                effectiveRunId,
                !string.IsNullOrWhiteSpace(runInfo?.SessionDefinitionCode)
                    ? runInfo.SessionDefinitionCode
                    : response.session?.code,
                bootstrapRun.RunStatus != RunLifecycleStatus.Unknown
                    ? bootstrapRun.RunStatus
                    : runInfo?.RunStatus ?? RunLifecycleStatus.Unknown,
                bootstrapRun.CurrentPeriodNumber,
                response.participant?.assignedGroupCode ?? runInfo?.AssignedGroupCode);

            var session = new SessionRuntimeModel(
                response.session?.sessionDefinitionId ?? 0,
                response.session?.code,
                response.session?.title,
                response.session?.description,
                SessionContractMapper.ToSessionStatus(response.session?.status),
                response.session?.configVersion ?? 0,
                response.session?.participantCountPlanned ?? 0,
                new ParsedSessionConfigModel(ParseDocument(
                    response.session?.sessionConfigJson,
                    "sessionConfigJson")));

            var participant = new ParticipantRuntimeModel(
                response.participant?.participantAccountId ?? 0,
                response.participant?.login,
                response.participant?.assignedGroupCode ?? runInfo?.AssignedGroupCode,
                new ParsedAssignedConfigModel(ParseDocument(
                    response.participant?.assignedConfigJson,
                    "assignedConfigJson")),
                ParseDocument(response.participant?.deviceBindingJson, "deviceBindingJson"));

            var templates = BuildSurveyTemplates(response.surveyTemplates);
            var payload = new BootstrapPayload(bootstrapRun, session, participant, templates);

            runtimeState = new ClientRuntimeState(
                tokenData,
                effectiveRunInfo,
                payload,
                true);

            return true;
        }

        public bool TryBuildFromSerializedBootstrap(
            AuthTokenData tokenData,
            AuthenticatedRunInfo runInfo,
            string serializedBootstrapPayload,
            out ClientRuntimeState runtimeState,
            out string error)
        {
            runtimeState = ClientRuntimeState.Empty;
            error = string.Empty;

            if (!_serializer.TryDeserialize(
                    serializedBootstrapPayload,
                    out BootstrapResponseDto response,
                    out var deserializeError))
            {
                error = $"Stored bootstrap payload deserialization failed: {deserializeError}";
                return false;
            }

            return TryBuild(tokenData, runInfo, response, out runtimeState, out error);
        }

        private IReadOnlyList<SurveyTemplateRuntimeModel> BuildSurveyTemplates(
            SurveyTemplateDto[] source)
        {
            if (source == null || source.Length == 0)
            {
                return Array.Empty<SurveyTemplateRuntimeModel>();
            }

            var result = new List<SurveyTemplateRuntimeModel>(source.Length);

            foreach (var template in source)
            {
                if (template == null)
                {
                    continue;
                }

                result.Add(new SurveyTemplateRuntimeModel(
                    template.id,
                    template.code,
                    template.title,
                    SessionContractMapper.ToSurveyTemplateKind(template.type),
                    template.version,
                    ParseDocument(template.templateJson, $"templateJson:{template.code}")));
            }

            return result;
        }

        private ParsedJsonDocument ParseDocument(string rawJson, string label)
        {
            if (string.IsNullOrWhiteSpace(rawJson))
            {
                return ParsedJsonDocument.Empty;
            }

            if (_jsonNodeParser.TryParse(rawJson, out var root, out var error))
            {
                return new ParsedJsonDocument(rawJson, root, false, true, string.Empty);
            }

            _logger.Warning($"Failed to parse {label}. {error}");
            return new ParsedJsonDocument(rawJson, JsonValue.Null, false, false, error);
        }
    }
}
