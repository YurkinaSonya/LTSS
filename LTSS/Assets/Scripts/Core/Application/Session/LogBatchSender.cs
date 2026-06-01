using System;
using Game.Core.Application.Networking;

namespace Game.Core.Application.Session
{
    public sealed class LogBatchSender : ILogBatchSender
    {
        private readonly IApiClient _apiClient;
        private readonly IJsonSerializer _serializer;

        public LogBatchSender(
            IApiClient apiClient,
            IJsonSerializer serializer)
        {
            _apiClient = apiClient;
            _serializer = serializer;
        }

        public void Send(
            string runId,
            string bearerToken,
            LogBatchRequestDto request,
            Action<ApiResponse<ServerAckDto>> onCompleted = null)
        {
            var path = $"api/run/{runId}/logs/batch";
            var payload = ParticipantWritePayloadBuilder.BuildLogBatchRequestJson(request);

            _apiClient.PostRaw<string>(
                path,
                payload,
                response =>
                {
                    onCompleted?.Invoke(ParticipantWriteResponseParser.ToAckResponse(response, _serializer));
                },
                ApiRequestOptions.WithBearer(bearerToken));
        }
    }
}
