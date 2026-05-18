using System;
using Game.Core.Application.Logging;
using Game.Core.Application.Networking;
using UnityEngine;

namespace Game.Core.Application.Session
{
    [Serializable]
    public sealed class PersistedAuthContextSnapshot
    {
        public string token;
        public string runId;
        public string sessionDefinitionCode;
        public string runStatus;
        public int currentPeriodNumber;
        public string assignedGroupCode;
        public string savedAtUtc;
        public bool canRestore;

        public AuthTokenData ToTokenData()
        {
            return new AuthTokenData(token, savedAtUtc);
        }

        public AuthenticatedRunInfo ToRunInfo()
        {
            return new AuthenticatedRunInfo(
                runId,
                sessionDefinitionCode,
                SessionContractMapper.ToRunStatus(runStatus),
                currentPeriodNumber,
                assignedGroupCode);
        }
    }

    [Serializable]
    public sealed class PersistedBootstrapSnapshot
    {
        public string runId;
        public int bootstrapVersion;
        public string rawBootstrapPayload;
        public string savedAtUtc;
        public bool canRestore;
    }

    public sealed class SessionPersistenceService : ISessionPersistenceService
    {
        private const string AuthContextKey = "session.auth_context";
        private const string BootstrapSnapshotKey = "session.bootstrap_snapshot";
        private const string PeriodSnapshotKey = "session.period_snapshot";
        private const string SessionFlowProgressKey = "session.flow_progress";

        private readonly IJsonSerializer _serializer;
        private readonly IAppLogger _logger;

        public SessionPersistenceService(
            IJsonSerializer serializer,
            IAppLogger logger)
        {
            _serializer = serializer;
            _logger = logger;
        }

        public void SaveAuthContext(AuthTokenData tokenData, AuthenticatedRunInfo runInfo)
        {
            if (tokenData == null || !tokenData.IsValid || runInfo == null || !runInfo.IsValid)
            {
                return;
            }

            var snapshot = new PersistedAuthContextSnapshot
            {
                token = tokenData.Token,
                runId = runInfo.RunId,
                sessionDefinitionCode = runInfo.SessionDefinitionCode,
                runStatus = SessionContractMapper.ToRunStatusCode(runInfo.RunStatus),
                currentPeriodNumber = runInfo.CurrentPeriodNumber,
                assignedGroupCode = runInfo.AssignedGroupCode,
                savedAtUtc = string.IsNullOrWhiteSpace(tokenData.SavedAtUtc)
                    ? DateTime.UtcNow.ToString("O")
                    : tokenData.SavedAtUtc,
                canRestore = true
            };

            PlayerPrefs.SetString(AuthContextKey, _serializer.Serialize(snapshot));
            PlayerPrefs.Save();
        }

        public bool TryLoadAuthContext(out PersistedAuthContextSnapshot snapshot)
        {
            snapshot = null;

            if (!PlayerPrefs.HasKey(AuthContextKey))
            {
                return false;
            }

            var raw = PlayerPrefs.GetString(AuthContextKey, string.Empty);

            if (!_serializer.TryDeserialize(raw, out snapshot, out var error)
                || snapshot == null
                || !snapshot.canRestore
                || string.IsNullOrWhiteSpace(snapshot.token))
            {
                _logger.Warning($"Stored auth context is invalid. {error}");
                ClearAuthContext();
                snapshot = null;
                return false;
            }

            return true;
        }

        public void ClearAuthContext()
        {
            PlayerPrefs.DeleteKey(AuthContextKey);
            PlayerPrefs.Save();
        }

        public void SaveBootstrapSnapshot(
            AuthenticatedRunInfo runInfo,
            int bootstrapVersion,
            string rawBootstrapPayload)
        {
            if (runInfo == null || !runInfo.IsValid || string.IsNullOrWhiteSpace(rawBootstrapPayload))
            {
                return;
            }

            var snapshot = new PersistedBootstrapSnapshot
            {
                runId = runInfo.RunId,
                bootstrapVersion = bootstrapVersion,
                rawBootstrapPayload = rawBootstrapPayload,
                savedAtUtc = DateTime.UtcNow.ToString("O"),
                canRestore = true
            };

            PlayerPrefs.SetString(BootstrapSnapshotKey, _serializer.Serialize(snapshot));
            PlayerPrefs.Save();
        }

        public bool TryLoadBootstrapSnapshot(out PersistedBootstrapSnapshot snapshot)
        {
            snapshot = null;

            if (!PlayerPrefs.HasKey(BootstrapSnapshotKey))
            {
                return false;
            }

            var raw = PlayerPrefs.GetString(BootstrapSnapshotKey, string.Empty);

            if (!_serializer.TryDeserialize(raw, out snapshot, out var error)
                || snapshot == null
                || !snapshot.canRestore
                || string.IsNullOrWhiteSpace(snapshot.rawBootstrapPayload))
            {
                _logger.Warning($"Stored bootstrap snapshot is invalid. {error}");
                ClearBootstrapSnapshot();
                snapshot = null;
                return false;
            }

            return true;
        }

        public void ClearBootstrapSnapshot()
        {
            PlayerPrefs.DeleteKey(BootstrapSnapshotKey);
            PlayerPrefs.Save();
        }

        public void SavePeriodSnapshot(
            string runId,
            int periodNumber,
            string flowState,
            string rawPeriodState,
            bool isCheckpointSubmitted)
        {
            if (string.IsNullOrWhiteSpace(runId)
                || periodNumber <= 0
                || string.IsNullOrWhiteSpace(rawPeriodState))
            {
                return;
            }

            var snapshot = new PersistedPeriodSnapshot
            {
                runId = runId,
                periodNumber = periodNumber,
                flowState = flowState ?? string.Empty,
                rawPeriodStateJson = rawPeriodState,
                savedAtUtc = DateTime.UtcNow.ToString("O"),
                isCheckpointSubmitted = isCheckpointSubmitted,
                canRestore = true
            };

            PlayerPrefs.SetString(PeriodSnapshotKey, _serializer.Serialize(snapshot));
            PlayerPrefs.Save();
        }

        public bool TryLoadPeriodSnapshot(out PersistedPeriodSnapshot snapshot)
        {
            snapshot = null;

            if (!PlayerPrefs.HasKey(PeriodSnapshotKey))
            {
                return false;
            }

            var raw = PlayerPrefs.GetString(PeriodSnapshotKey, string.Empty);

            if (!_serializer.TryDeserialize(raw, out snapshot, out var error)
                || snapshot == null
                || !snapshot.canRestore
                || string.IsNullOrWhiteSpace(snapshot.runId)
                || snapshot.periodNumber <= 0
                || string.IsNullOrWhiteSpace(snapshot.rawPeriodStateJson))
            {
                _logger.Warning($"Stored period snapshot is invalid. {error}");
                ClearPeriodSnapshot();
                snapshot = null;
                return false;
            }

            return true;
        }

        public void ClearPeriodSnapshot()
        {
            PlayerPrefs.DeleteKey(PeriodSnapshotKey);
            PlayerPrefs.Save();
        }

        public void SaveSessionFlowProgress(
            string runId,
            int sessionConfigVersion,
            int schemaVersion,
            SessionFlowProgressState progress)
        {
            if (string.IsNullOrWhiteSpace(runId) || progress == null)
            {
                return;
            }

            var activeStep = progress.ActiveStep ?? SessionFlowStepDescriptor.Empty;
            var snapshot = new PersistedSessionFlowProgressSnapshot
            {
                runId = runId,
                sessionConfigVersion = sessionConfigVersion,
                schemaVersion = schemaVersion,
                completedPreSessionStepKeys = ToArray(progress.CompletedPreSessionStepKeys),
                completedPeriodContentBlockKeys = ToArray(progress.CompletedPeriodContentBlockKeys),
                completedPostPeriodSurveyKeys = ToArray(progress.CompletedPostPeriodSurveyKeys),
                completedInterPeriodBlockKeys = ToArray(progress.CompletedInterPeriodBlockKeys),
                completedPostSessionStepKeys = ToArray(progress.CompletedPostSessionStepKeys),
                completedPeriodNumbers = ToArray(progress.CompletedPeriodNumbers),
                activePeriodNumber = progress.ActivePeriodNumber,
                activeStepKey = activeStep.Key,
                activeStepScope = activeStep.Scope.ToString(),
                activeStepType = activeStep.Type.ToString(),
                activeStepTitle = activeStep.Title,
                activeStepRequired = activeStep.IsRequired,
                isPostSessionCompleted = progress.IsPostSessionCompleted,
                isSessionCompleted = progress.IsSessionCompleted,
                savedAtUtc = DateTime.UtcNow.ToString("O"),
                canRestore = true
            };

            PlayerPrefs.SetString(SessionFlowProgressKey, _serializer.Serialize(snapshot));
            PlayerPrefs.Save();
        }

        public bool TryLoadSessionFlowProgress(out PersistedSessionFlowProgressSnapshot snapshot)
        {
            snapshot = null;

            if (!PlayerPrefs.HasKey(SessionFlowProgressKey))
            {
                return false;
            }

            var raw = PlayerPrefs.GetString(SessionFlowProgressKey, string.Empty);

            if (!_serializer.TryDeserialize(raw, out snapshot, out var error)
                || snapshot == null
                || !snapshot.canRestore
                || string.IsNullOrWhiteSpace(snapshot.runId))
            {
                _logger.Warning($"Stored session flow progress is invalid. {error}");
                ClearSessionFlowProgress();
                snapshot = null;
                return false;
            }

            return true;
        }

        public void ClearSessionFlowProgress()
        {
            PlayerPrefs.DeleteKey(SessionFlowProgressKey);
            PlayerPrefs.Save();
        }

        public void ClearAll()
        {
            ClearAuthContext();
            ClearBootstrapSnapshot();
            ClearPeriodSnapshot();
            ClearSessionFlowProgress();
        }

        private static string[] ToArray(System.Collections.Generic.IReadOnlyList<string> source)
        {
            if (source == null || source.Count == 0)
            {
                return Array.Empty<string>();
            }

            var result = new string[source.Count];

            for (var index = 0; index < source.Count; index++)
            {
                result[index] = source[index] ?? string.Empty;
            }

            return result;
        }

        private static int[] ToArray(System.Collections.Generic.IReadOnlyList<int> source)
        {
            if (source == null || source.Count == 0)
            {
                return Array.Empty<int>();
            }

            var result = new int[source.Count];

            for (var index = 0; index < source.Count; index++)
            {
                result[index] = source[index];
            }

            return result;
        }
    }
}
