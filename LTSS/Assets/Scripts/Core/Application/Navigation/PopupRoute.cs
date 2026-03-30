namespace Game.Core.Application.Navigation
{
    public sealed class PopupRoute
    {
        public Enums.PopupType Type { get; }
        public string Source { get; }

        public PopupRoute(Enums.PopupType type, string source = null)
        {
            Type = type;
            Source = source ?? string.Empty;
        }

        public bool IsEquivalentTo(PopupRoute other)
        {
            if (other == null)
            {
                return false;
            }

            return Type == other.Type && Source == other.Source;
        }
    }
}
