using UnityEngine;

namespace Game.Core.Application.Networking
{
    public sealed class JsonUtilitySerializer : IJsonSerializer
    {
        public string Serialize<T>(T value)
        {
            return JsonUtility.ToJson(value);
        }

        public T Deserialize<T>(string json)
        {
            return JsonUtility.FromJson<T>(json);
        }
    }
}
