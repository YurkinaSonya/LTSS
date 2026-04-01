using System;
using Game.Core.Application.Networking;

namespace Game.Core.Application.Session
{
    public sealed class RunApiClient : IRunApiClient
    {
        private const string CurrentRunPath = "api/run/current";

        private readonly IApiClient _apiClient;
        private readonly IJsonSerializer _serializer;

        public RunApiClient(
            IApiClient apiClient,
            IJsonSerializer serializer)
        {
            _apiClient = apiClient;
            _serializer = serializer;
        }

        public void GetCurrent(
            string bearerToken,
            Action<ApiResponse<RunInfoDto>> onCompleted = null)
        {
            _apiClient.Get<string>(
                CurrentRunPath,
                response =>
                {
                    if (!response.IsSuccess)
                    {
                        onCompleted?.Invoke(new ApiResponse<RunInfoDto>(
                            false,
                            response.StatusCode,
                            response.Error,
                            response.RawResponse,
                            null));
                        return;
                    }

                    if (TryParseCurrentRun(response.Payload, out var runInfo, out var error))
                    {
                        onCompleted?.Invoke(new ApiResponse<RunInfoDto>(
                            true,
                            response.StatusCode,
                            string.Empty,
                            response.RawResponse,
                            runInfo));
                        return;
                    }

                    onCompleted?.Invoke(new ApiResponse<RunInfoDto>(
                        false,
                        response.StatusCode,
                        $"Current run response parsing failed: {error}",
                        response.RawResponse,
                        null));
                },
                ApiRequestOptions.WithBearer(bearerToken));
        }

        public void GetBootstrap(
            string runId,
            string bearerToken,
            Action<ApiResponse<BootstrapResponseDto>> onCompleted = null)
        {
            var path = $"api/run/{runId}/bootstrap";

            _apiClient.Get<string>(
                path,
                response =>
                {
                    if (!response.IsSuccess)
                    {
                        onCompleted?.Invoke(new ApiResponse<BootstrapResponseDto>(
                            false,
                            response.StatusCode,
                            response.Error,
                            response.RawResponse,
                            null));
                        return;
                    }

                    if (!_serializer.TryDeserialize(
                            response.Payload,
                            out BootstrapResponseDto payload,
                            out var error))
                    {
                        onCompleted?.Invoke(new ApiResponse<BootstrapResponseDto>(
                            false,
                            response.StatusCode,
                            $"Bootstrap response parsing failed: {error}",
                            response.RawResponse,
                            null));
                        return;
                    }

                    onCompleted?.Invoke(new ApiResponse<BootstrapResponseDto>(
                        true,
                        response.StatusCode,
                        string.Empty,
                        response.RawResponse,
                        payload));
                },
                ApiRequestOptions.WithBearer(bearerToken));
        }

        private bool TryParseCurrentRun(string rawResponse, out RunInfoDto runInfo, out string error)
        {
            runInfo = null;
            error = string.Empty;

            if (_serializer.TryDeserialize(
                    rawResponse,
                    out RunCurrentEnvelopeDto envelope,
                    out var envelopeError)
                && envelope != null
                && envelope.run != null
                && !string.IsNullOrWhiteSpace(envelope.run.runId))
            {
                runInfo = envelope.run;
                return true;
            }

            if (_serializer.TryDeserialize(
                    rawResponse,
                    out RunInfoDto directPayload,
                    out var directError)
                && directPayload != null
                && !string.IsNullOrWhiteSpace(directPayload.runId))
            {
                runInfo = directPayload;
                return true;
            }

            error = !string.IsNullOrWhiteSpace(envelopeError)
                ? envelopeError
                : directError;

            return false;
        }
    }
}
