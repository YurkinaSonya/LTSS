using System;

namespace Game.Core.Application.Networking
{
    public interface IApiClient
    {
        void Get<TResponse>(
            string relativePath,
            Action<ApiResponse<TResponse>> onCompleted = null,
            ApiRequestOptions options = null);

        void Post<TRequest, TResponse>(
            string relativePath,
            TRequest body,
            Action<ApiResponse<TResponse>> onCompleted = null,
            ApiRequestOptions options = null);
    }
}
