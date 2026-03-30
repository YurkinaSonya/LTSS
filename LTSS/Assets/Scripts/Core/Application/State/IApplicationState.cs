namespace Game.Core.Application.State
{
    public interface IApplicationState
    {
        AppStateId StateId { get; }
        void Enter();
        void Exit();
    }
}
