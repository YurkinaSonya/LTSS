using System;
using Game.Core.Application.Networking;

namespace Game.Core.Application.Session
{
    public sealed class SurveySubmissionSender : ISurveySubmissionSender
    {
        private readonly IApiClient _apiClient;
        private readonly IJsonSerializer _serializer;

        public SurveySubmissionSender(
            IApiClient apiClient,
            IJsonSerializer serializer)
        {
            _apiClient = apiClient;
            _serializer = serializer;
        }

        public void Send(
            string runId,
            string bearerToken,
            SurveySubmissionRequestDto request,
            Action<ApiResponse<ServerAckDto>> onCompleted = null)
        {
            var path = $"api/run/{runId}/surveys/submit";
            var payload = ParticipantWritePayloadBuilder.BuildSurveySubmissionRequestJson(request);

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
