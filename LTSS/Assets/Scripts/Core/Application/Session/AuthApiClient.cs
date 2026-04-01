using System;
using Game.Core.Application.Networking;

namespace Game.Core.Application.Session
{
    public sealed class AuthApiClient : IAuthApiClient
    {
        private const string LoginPath = "api/auth/login";

        private readonly IApiClient _apiClient;
        private readonly IJsonSerializer _serializer;

        public AuthApiClient(
            IApiClient apiClient,
            IJsonSerializer serializer)
        {
            _apiClient = apiClient;
            _serializer = serializer;
        }

        public void Login(
            LoginRequestDto request,
            Action<ApiResponse<LoginResponseDto>> onCompleted = null)
        {
            _apiClient.Post<LoginRequestDto, string>(
                LoginPath,
                request,
                response =>
                {
                    if (!response.IsSuccess)
                    {
                        onCompleted?.Invoke(new ApiResponse<LoginResponseDto>(
                            false,
                            response.StatusCode,
                            response.Error,
                            response.RawResponse,
                            null));
                        return;
                    }

                    if (!_serializer.TryDeserialize(
                            response.Payload,
                            out LoginResponseDto payload,
                            out var error))
                    {
                        onCompleted?.Invoke(new ApiResponse<LoginResponseDto>(
                            false,
                            response.StatusCode,
                            $"Login response parsing failed: {error}",
                            response.RawResponse,
                            null));
                        return;
                    }

                    onCompleted?.Invoke(new ApiResponse<LoginResponseDto>(
                        true,
                        response.StatusCode,
                        string.Empty,
                        response.RawResponse,
                        payload));
                });
        }
    }
}
