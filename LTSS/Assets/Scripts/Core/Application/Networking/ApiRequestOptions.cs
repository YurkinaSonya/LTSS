using System.Collections.Generic;

namespace Game.Core.Application.Networking
{
    public sealed class ApiRequestOptions
    {
        public static ApiRequestOptions None { get; } = new ApiRequestOptions();

        public string BearerToken { get; }
        public IReadOnlyDictionary<string, string> Headers { get; }

        public ApiRequestOptions(
            string bearerToken = null,
            IReadOnlyDictionary<string, string> headers = null)
        {
            BearerToken = bearerToken ?? string.Empty;
            Headers = headers;
        }

        public static ApiRequestOptions WithBearer(string bearerToken)
        {
            return new ApiRequestOptions(bearerToken);
        }
    }
}
