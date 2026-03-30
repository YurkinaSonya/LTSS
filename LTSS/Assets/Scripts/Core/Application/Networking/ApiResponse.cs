namespace Game.Core.Application.Networking
{
    public sealed class ApiResponse<T>
    {
        public bool IsSuccess { get; }
        public long StatusCode { get; }
        public string Error { get; }
        public string RawResponse { get; }
        public T Payload { get; }

        public ApiResponse(
            bool isSuccess,
            long statusCode,
            string error,
            string rawResponse,
            T payload)
        {
            IsSuccess = isSuccess;
            StatusCode = statusCode;
            Error = error ?? string.Empty;
            RawResponse = rawResponse ?? string.Empty;
            Payload = payload;
        }
    }
}
