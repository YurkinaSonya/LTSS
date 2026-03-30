namespace Game.Core.Feature
{
    /// <summary>
    /// Defines a controlled system initialization contract.
    /// This interface intentionally replaces Zenject's IInitializable
    /// to centralize initialization order within the GameBootstrapper.
    /// </summary>
    public interface IGameFeatureInitializable
    {
        /// <summary>
        /// Performs deterministic, container-managed startup logic.
        /// Implementations should avoid Unity lifecycle methods.
        /// </summary>
        void Initialize();
    }
}
