using UnityEngine;
using System;

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

        public bool TryDeserialize<T>(string json, out T value, out string error)
        {
            value = default;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(json))
            {
                error = "JSON payload is empty.";
                return false;
            }

            try
            {
                value = JsonUtility.FromJson<T>(json);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }
    }
}
