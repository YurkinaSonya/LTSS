using System;
using Game.Core.Application.Networking;

namespace Game.Core.Application.Session
{
    public sealed class CheckpointSender : ICheckpointSender
    {
        private readonly IApiClient _apiClient;
        private readonly IJsonSerializer _serializer;

        public CheckpointSender(
            IApiClient apiClient,
            IJsonSerializer serializer)
        {
            _apiClient = apiClient;
            _serializer = serializer;
        }

        public void Send(
            string runId,
            string bearerToken,
            CheckpointRequestDto request,
            Action<ApiResponse<ServerAckDto>> onCompleted = null)
        {
            var path = $"api/run/{runId}/checkpoint";

            _apiClient.Post<CheckpointRequestDto, string>(
                path,
                request,
                response =>
                {
                    if (!response.IsSuccess)
                    {
                        onCompleted?.Invoke(new ApiResponse<ServerAckDto>(
                            false,
                            response.StatusCode,
                            response.Error,
                            response.RawResponse,
                            null));
                        return;
                    }

                    if (string.IsNullOrWhiteSpace(response.Payload))
                    {
                        onCompleted?.Invoke(new ApiResponse<ServerAckDto>(
                            true,
                            response.StatusCode,
                            string.Empty,
                            response.RawResponse,
                            new ServerAckDto
                            {
                                status = "ok",
                                message = string.Empty
                            }));
                        return;
                    }

                    if (!_serializer.TryDeserialize(
                            response.Payload,
                            out ServerAckDto payload,
                            out _)
                        || payload == null)
                    {
                        payload = new ServerAckDto
                        {
                            status = "ok",
                            message = response.Payload
                        };
                    }

                    onCompleted?.Invoke(new ApiResponse<ServerAckDto>(
                        true,
                        response.StatusCode,
                        string.Empty,
                        response.RawResponse,
                        payload));
                },
                ApiRequestOptions.WithBearer(bearerToken));
        }
    }
}
