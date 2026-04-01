namespace Game.Core.Application.Session
{
    public interface ISessionPersistenceService
    {
        void SaveAuthContext(AuthTokenData tokenData, AuthenticatedRunInfo runInfo);
        bool TryLoadAuthContext(out PersistedAuthContextSnapshot snapshot);
        void ClearAuthContext();
        void SaveBootstrapSnapshot(
            AuthenticatedRunInfo runInfo,
            int bootstrapVersion,
            string rawBootstrapPayload);
        bool TryLoadBootstrapSnapshot(out PersistedBootstrapSnapshot snapshot);
        void ClearBootstrapSnapshot();
        void ClearAll();
    }
}
