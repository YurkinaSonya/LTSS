namespace Game.Core.Application.Session
{
    public interface IJsonNodeParser
    {
        bool TryParse(string json, out JsonValue value, out string error);
    }
}
