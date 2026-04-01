namespace Game.Core.Application.Session
{
    public interface ISessionRuntimeFactory
    {
        bool TryBuild(
            AuthTokenData tokenData,
            AuthenticatedRunInfo runInfo,
            BootstrapResponseDto response,
            out ClientRuntimeState runtimeState,
            out string error);

        bool TryBuildFromSerializedBootstrap(
            AuthTokenData tokenData,
            AuthenticatedRunInfo runInfo,
            string serializedBootstrapPayload,
            out ClientRuntimeState runtimeState,
            out string error);
    }
}
