using System;
using System.Collections.Generic;
using Game.Core.Application.Networking;
using Game.Core.Application.Session;
using Game.Core.Events;
using Zenject;

namespace Game.Core.Application.Logging
{
    public interface IRunTelemetryService
    {
        void FlushPending(string reason = null);
    }

    public sealed class RunTelemetryService : IRunTelemetryService, IInitializable
    {
        private const int FlushThreshold = 12;

        private readonly ISessionCoordinator _sessionCoordinator;
        private readonly ILogBatchSender _logBatchSender;
        private readonly IJsonSerializer _serializer;
        private readonly IEventAggregator _eventAggregator;
        private readonly IAppLogger _logger;

        private readonly List<PendingTelemetryEvent> _pendingEvents = new List<PendingTelemetryEvent>();
        private bool _isFlushing;

        public RunTelemetryService(
            ISessionCoordinator sessionCoordinator,
            ILogBatchSender logBatchSender,
            IJsonSerializer serializer,
            IEventAggregator eventAggregator,
            IAppLogger logger)
        {
            _sessionCoordinator = sessionCoordinator;
            _logBatchSender = logBatchSender;
            _serializer = serializer;
            _eventAggregator = eventAggregator;
            _logger = logger;
        }

        public void Initialize()
        {
            _eventAggregator?.Subscribe<EventsProvider.UserActionLoggedEvent>(OnUserActionLogged);
            _eventAggregator?.Subscribe<EventsProvider.ClientRuntimeStateChangedEvent>(OnClientRuntimeStateChanged);
        }

        public void FlushPending(string reason = null)
        {
            TryFlush(force: true, reason: reason);
        }

        private void OnUserActionLogged(EventsProvider.UserActionLoggedEvent eventData)
        {
            var entry = eventData != null ? eventData.Entry : null;

            if (!ShouldCapture(entry))
            {
                return;
            }

            var runId = ResolveRunId(entry);

            if (string.IsNullOrWhiteSpace(runId))
            {
                return;
            }

            var periodNumber = ResolvePeriodNumber(entry);
            _pendingEvents.Add(new PendingTelemetryEvent(
                runId,
                ResolveBatchType(periodNumber),
                periodNumber,
                BuildTelemetryEvent(entry, runId, periodNumber)));

            if (_pendingEvents.Count > 256)
            {
                _pendingEvents.RemoveAt(0);
            }

            TryFlush(force: false, reason: "threshold");
        }

        private void OnClientRuntimeStateChanged(EventsProvider.ClientRuntimeStateChangedEvent eventData)
        {
            var runtimeState = eventData != null ? eventData.RuntimeState : ClientRuntimeState.Empty;
            var activeRunId = runtimeState != null && runtimeState.HasSession
                ? runtimeState.AuthenticatedRun.RunId
                : string.Empty;

            if (string.IsNullOrWhiteSpace(activeRunId))
            {
                return;
            }

            _pendingEvents.RemoveAll(candidate =>
                candidate == null
                || string.IsNullOrWhiteSpace(candidate.RunId)
                || !string.Equals(candidate.RunId, activeRunId, StringComparison.Ordinal));

            TryFlush(force: true, reason: "runtime_changed");
        }

        private void TryFlush(bool force, string reason)
        {
            if (_isFlushing || _pendingEvents.Count == 0)
            {
                return;
            }

            var runtimeState = _sessionCoordinator != null
                ? _sessionCoordinator.CurrentRuntime
                : ClientRuntimeState.Empty;

            if (runtimeState == null
                || !runtimeState.HasSession
                || string.IsNullOrWhiteSpace(runtimeState.AuthToken.Token)
                || string.IsNullOrWhiteSpace(runtimeState.AuthenticatedRun.RunId))
            {
                return;
            }

            var currentRunId = runtimeState.AuthenticatedRun.RunId;
            var candidates = new List<PendingTelemetryEvent>();

            for (var index = 0; index < _pendingEvents.Count; index++)
            {
                var candidate = _pendingEvents[index];

                if (candidate != null
                    && string.Equals(candidate.RunId, currentRunId, StringComparison.Ordinal))
                {
                    candidates.Add(candidate);
                }
            }

            if (candidates.Count == 0)
            {
                return;
            }

            if (!force && candidates.Count < FlushThreshold)
            {
                return;
            }

            _pendingEvents.RemoveAll(candidate =>
                candidate != null
                && string.Equals(candidate.RunId, currentRunId, StringComparison.Ordinal));

            var pendingBatches = BuildBatches(candidates);

            if (pendingBatches.Count == 0)
            {
                return;
            }

            _isFlushing = true;
            SendBatchRecursive(
                runtimeState.AuthenticatedRun.RunId,
                runtimeState.AuthToken.Token,
                pendingBatches,
                0,
                reason);
        }

        private void SendBatchRecursive(
            string runId,
            string bearerToken,
            List<PendingLogBatch> batches,
            int index,
            string reason)
        {
            if (index >= batches.Count)
            {
                _isFlushing = false;
                TryFlush(force: false, reason: "follow_up");
                return;
            }

            var batch = batches[index];

            _logBatchSender.Send(
                runId,
                bearerToken,
                batch.Request,
                response =>
                {
                    if (response == null || !response.IsSuccess)
                    {
                        RestorePendingEvents(batches, index);
                        _isFlushing = false;

                        var errorDetail = response == null
                            ? "empty response"
                            : string.IsNullOrWhiteSpace(response.Error)
                                ? $"HTTP {response.StatusCode}"
                                : response.Error;

                        _logger.Warning(
                            $"Client telemetry batch send failed for run '{runId}'. Reason: {reason ?? "unknown"}, Detail: {errorDetail}.");
                        return;
                    }

                    SendBatchRecursive(runId, bearerToken, batches, index + 1, reason);
                });
        }

        private void RestorePendingEvents(List<PendingLogBatch> batches, int failedIndex)
        {
            var toRestore = new List<PendingTelemetryEvent>();

            for (var index = failedIndex; index < batches.Count; index++)
            {
                var batch = batches[index];

                if (batch == null || batch.SourceEvents == null)
                {
                    continue;
                }

                toRestore.AddRange(batch.SourceEvents);
            }

            if (toRestore.Count == 0)
            {
                return;
            }

            _pendingEvents.InsertRange(0, toRestore);
        }

        private List<PendingLogBatch> BuildBatches(IReadOnlyList<PendingTelemetryEvent> events)
        {
            var result = new List<PendingLogBatch>();

            if (events == null || events.Count == 0)
            {
                return result;
            }

            for (var index = 0; index < events.Count; index++)
            {
                var candidate = events[index];

                if (candidate == null || candidate.EventPayload == null)
                {
                    continue;
                }

                PendingLogBatch existingBatch = null;

                for (var batchIndex = 0; batchIndex < result.Count; batchIndex++)
                {
                    var batch = result[batchIndex];

                    if (batch != null
                        && batch.Matches(candidate.RunId, candidate.BatchType, candidate.HasPeriodNumber, candidate.PeriodNumber))
                    {
                        existingBatch = batch;
                        break;
                    }
                }

                if (existingBatch == null)
                {
                    existingBatch = new PendingLogBatch(
                        candidate.RunId,
                        new LogBatchRequestDto
                        {
                            batchType = candidate.BatchType,
                            hasPeriodNumber = candidate.HasPeriodNumber,
                            periodNumber = candidate.PeriodNumber
                        });
                    result.Add(existingBatch);
                }

                existingBatch.SourceEvents.Add(candidate);
            }

            for (var index = 0; index < result.Count; index++)
            {
                var batch = result[index];
                var payload = new TelemetryBatchPayloadDto
                {
                    events = BuildPayloadArray(batch.SourceEvents)
                };
                batch.Request.payloadJson = _serializer.Serialize(payload);
            }

            return result;
        }

        private static TelemetryEventDto[] BuildPayloadArray(IReadOnlyList<PendingTelemetryEvent> events)
        {
            if (events == null || events.Count == 0)
            {
                return Array.Empty<TelemetryEventDto>();
            }

            var result = new TelemetryEventDto[events.Count];

            for (var index = 0; index < events.Count; index++)
            {
                result[index] = events[index] != null
                    ? events[index].EventPayload
                    : null;
            }

            return result;
        }

        private static bool ShouldCapture(UserActionLogEntry entry)
        {
            if (entry == null)
            {
                return false;
            }

            switch (entry.Type)
            {
                case UserActionType.HttpRequest:
                case UserActionType.HttpResponse:
                case UserActionType.HttpError:
                case UserActionType.PopupClosed:
                    return false;
                default:
                    return true;
            }
        }

        private string ResolveRunId(UserActionLogEntry entry)
        {
            var runtimeState = _sessionCoordinator != null
                ? _sessionCoordinator.CurrentRuntime
                : ClientRuntimeState.Empty;

            if (runtimeState != null
                && runtimeState.HasSession
                && !string.IsNullOrWhiteSpace(runtimeState.AuthenticatedRun.RunId))
            {
                return runtimeState.AuthenticatedRun.RunId;
            }

            return TryGetMetadataValue(entry, "runId");
        }

        private static int? ResolvePeriodNumber(UserActionLogEntry entry)
        {
            if (!TryParsePeriodNumber(TryGetMetadataValue(entry, "period"), out var periodNumber)
                && !TryParsePeriodNumber(TryGetMetadataValue(entry, "periodNumber"), out periodNumber))
            {
                return null;
            }

            return periodNumber > 0 ? periodNumber : (int?)null;
        }

        private static TelemetryEventDto BuildTelemetryEvent(
            UserActionLogEntry entry,
            string runId,
            int? periodNumber)
        {
            return new TelemetryEventDto
            {
                type = entry != null ? entry.Name : string.Empty,
                category = entry != null ? entry.Type.ToString() : string.Empty,
                ts = entry != null
                    ? new DateTimeOffset(entry.TimestampUtc).ToUnixTimeSeconds()
                    : DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                runId = runId ?? string.Empty,
                hasPeriodNumber = periodNumber.HasValue,
                periodNumber = periodNumber ?? 0,
                metadata = BuildMetadata(entry != null ? entry.Metadata : null)
            };
        }

        private static TelemetryMetadataEntryDto[] BuildMetadata(IReadOnlyDictionary<string, string> metadata)
        {
            if (metadata == null || metadata.Count == 0)
            {
                return Array.Empty<TelemetryMetadataEntryDto>();
            }

            var result = new TelemetryMetadataEntryDto[metadata.Count];
            var index = 0;

            foreach (var pair in metadata)
            {
                result[index++] = new TelemetryMetadataEntryDto
                {
                    key = pair.Key ?? string.Empty,
                    value = pair.Value ?? string.Empty
                };
            }

            return result;
        }

        private static bool TryParsePeriodNumber(string rawValue, out int periodNumber)
        {
            periodNumber = 0;
            return !string.IsNullOrWhiteSpace(rawValue)
                   && int.TryParse(rawValue, out periodNumber)
                   && periodNumber > 0;
        }

        private static string ResolveBatchType(int? periodNumber)
        {
            return periodNumber.HasValue && periodNumber.Value > 0
                ? "period-events"
                : "session-events";
        }

        private static string TryGetMetadataValue(UserActionLogEntry entry, string key)
        {
            if (entry == null
                || entry.Metadata == null
                || string.IsNullOrWhiteSpace(key)
                || !entry.Metadata.TryGetValue(key, out var value))
            {
                return string.Empty;
            }

            return value ?? string.Empty;
        }

        private sealed class PendingTelemetryEvent
        {
            public string RunId { get; }
            public string BatchType { get; }
            public bool HasPeriodNumber { get; }
            public int PeriodNumber { get; }
            public TelemetryEventDto EventPayload { get; }

            public PendingTelemetryEvent(
                string runId,
                string batchType,
                int? periodNumber,
                TelemetryEventDto eventPayload)
            {
                RunId = runId ?? string.Empty;
                BatchType = string.IsNullOrWhiteSpace(batchType) ? "session-events" : batchType;
                HasPeriodNumber = periodNumber.HasValue && periodNumber.Value > 0;
                PeriodNumber = HasPeriodNumber ? periodNumber.Value : 0;
                EventPayload = eventPayload;
            }
        }

        private sealed class PendingLogBatch
        {
            public string RunId { get; }
            public LogBatchRequestDto Request { get; }
            public List<PendingTelemetryEvent> SourceEvents { get; }

            public PendingLogBatch(
                string runId,
                LogBatchRequestDto request)
            {
                RunId = runId ?? string.Empty;
                Request = request ?? new LogBatchRequestDto();
                SourceEvents = new List<PendingTelemetryEvent>();
            }

            public bool Matches(
                string runId,
                string batchType,
                bool hasPeriodNumber,
                int periodNumber)
            {
                return string.Equals(RunId, runId ?? string.Empty, StringComparison.Ordinal)
                       && string.Equals(Request.batchType, batchType ?? string.Empty, StringComparison.Ordinal)
                       && Request.hasPeriodNumber == hasPeriodNumber
                       && (!hasPeriodNumber || Request.periodNumber == periodNumber);
            }
        }

        [Serializable]
        private sealed class TelemetryBatchPayloadDto
        {
            public TelemetryEventDto[] events;
        }

        [Serializable]
        private sealed class TelemetryEventDto
        {
            public string type;
            public string category;
            public long ts;
            public string runId;
            public bool hasPeriodNumber;
            public int periodNumber;
            public TelemetryMetadataEntryDto[] metadata;
        }

        [Serializable]
        private sealed class TelemetryMetadataEntryDto
        {
            public string key;
            public string value;
        }
    }
}
