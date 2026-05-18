namespace Game.Core.Application.Session
{
    public interface ISessionConfigRuntimeBuilder
    {
        bool TryBuild(
            ParsedJsonDocument document,
            out SessionConfigRuntime runtime,
            out string error);
    }
}
