namespace Game.Core.Application.Networking
{
    public interface IJsonSerializer
    {
        string Serialize<T>(T value);
        T Deserialize<T>(string json);
    }
}
