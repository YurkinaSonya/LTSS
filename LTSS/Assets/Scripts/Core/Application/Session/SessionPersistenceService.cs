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

        public void ClearAll()
        {
            ClearAuthContext();
            ClearBootstrapSnapshot();
        }
    }
}
