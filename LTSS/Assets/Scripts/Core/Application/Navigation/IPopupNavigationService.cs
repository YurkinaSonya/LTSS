namespace Game.Core.Application.Navigation
{
    public interface IPopupNavigationService
    {
        void Push(Enums.PopupType type, string source = null);
        void Push(PopupRoute route);
        void Pop(string source = null);
        void Clear(string source = null);
    }
}
