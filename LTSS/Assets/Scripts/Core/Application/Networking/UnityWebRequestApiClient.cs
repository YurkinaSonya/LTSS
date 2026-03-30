using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Game.Core;
using Game.Core.Application.Logging;
using Game.Core.Events;
using UnityEngine.Networking;

namespace Game.Core.Application.Networking
{
    public sealed class UnityWebRequestApiClient : IApiClient
    {
        private readonly ApiServiceSettings _settings;
        private readonly IJsonSerializer _serializer;
        private readonly CoroutineRunner _coroutineRunner;
        private readonly IAppLogger _logger;
        private readonly IUserActionLogger _userActionLogger;
        private readonly IEventAggregator _eventAggregator;

        public UnityWebRequestApiClient(
            ApiServiceSettings settings,
            IJsonSerializer serializer,
            CoroutineRunner coroutineRunner,
            IAppLogger logger,
            IUserActionLogger userActionLogger,
            IEventAggregator eventAggregator)
        {
            _settings = settings;
            _serializer = serializer;
            _coroutineRunner = coroutineRunner;
            _logger = logger;
            _userActionLogger = userActionLogger;
            _eventAggregator = eventAggregator;
        }

        public void Get<TResponse>(
            string relativePath,
            Action<ApiResponse<TResponse>> onCompleted = null)
        {
            if (_coroutineRunner == null)
            {
                _logger.Error("CoroutineRunner is not available for HTTP GET request execution.");
                return;
            }

            _coroutineRunner.StartCoroutine(
                SendRequest(relativePath, UnityWebRequest.kHttpVerbGET, null, onCompleted));
        }

        public void Post<TRequest, TResponse>(
            string relativePath,
            TRequest body,
            Action<ApiResponse<TResponse>> onCompleted = null)
        {
            if (_coroutineRunner == null)
            {
                _logger.Error("CoroutineRunner is not available for HTTP POST request execution.");
                return;
            }

            var payload = body == null ? string.Empty : _serializer.Serialize(body);

            _coroutineRunner.StartCoroutine(
                SendRequest(relativePath, UnityWebRequest.kHttpVerbPOST, payload, onCompleted));
        }

        private IEnumerator SendRequest<TResponse>(
            string relativePath,
            string method,
            string payload,
            Action<ApiResponse<TResponse>> onCompleted)
        {
            var requestUrl = BuildUrl(relativePath);

            using (var request = new UnityWebRequest(requestUrl, method))
            {
                request.timeout = Math.Max(1, _settings.TimeoutSeconds);
                request.downloadHandler = new DownloadHandlerBuffer();

                if (!string.IsNullOrEmpty(payload))
                {
                    request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload));
                    request.SetRequestHeader("Content-Type", "application/json");
                }

                ApplyHeaders(request);

                _userActionLogger.Log(
                    UserActionType.HttpRequest,
                    requestUrl,
                    new Dictionary<string, string>
                    {
                        { "method", method }
                    });

                yield return request.SendWebRequest();

                var rawResponse = request.downloadHandler != null
                    ? request.downloadHandler.text
                    : string.Empty;

                var isSuccess = request.result == UnityWebRequest.Result.Success
                    && request.responseCode >= 200
                    && request.responseCode < 300;

                var response = BuildResponse<TResponse>(
                    isSuccess,
                    request.responseCode,
                    request.error,
                    rawResponse);

                var metadata = new Dictionary<string, string>
                {
                    { "method", method },
                    { "statusCode", request.responseCode.ToString() }
                };

                if (!string.IsNullOrEmpty(relativePath))
                {
                    metadata["path"] = relativePath;
                }

                _userActionLogger.Log(
                    isSuccess ? UserActionType.HttpResponse : UserActionType.HttpError,
                    requestUrl,
                    metadata);

                _eventAggregator.Publish(new EventsProvider.ApiRequestCompletedEvent(
                    method,
                    requestUrl,
                    isSuccess,
                    request.responseCode,
                    request.error));

                if (!isSuccess)
                {
                    _logger.Warning(
                        $"HTTP request failed. Method: {method}, Url: {requestUrl}, " +
                        $"Status: {request.responseCode}, Error: {request.error}");
                }

                onCompleted?.Invoke(response);
            }
        }

        private ApiResponse<TResponse> BuildResponse<TResponse>(
            bool isSuccess,
            long statusCode,
            string error,
            string rawResponse)
        {
            if (!isSuccess)
            {
                return new ApiResponse<TResponse>(
                    false,
                    statusCode,
                    error,
                    rawResponse,
                    default);
            }

            try
            {
                if (typeof(TResponse) == typeof(string))
                {
                    return new ApiResponse<TResponse>(
                        true,
                        statusCode,
                        string.Empty,
                        rawResponse,
                        (TResponse)(object)rawResponse);
                }

                if (string.IsNullOrEmpty(rawResponse))
                {
                    return new ApiResponse<TResponse>(
                        true,
                        statusCode,
                        string.Empty,
                        rawResponse,
                        default);
                }

                return new ApiResponse<TResponse>(
                    true,
                    statusCode,
                    string.Empty,
                    rawResponse,
                    _serializer.Deserialize<TResponse>(rawResponse));
            }
            catch (Exception exception)
            {
                _logger.Error("Failed to deserialize API response.", exception);

                return new ApiResponse<TResponse>(
                    false,
                    statusCode,
                    $"Deserialization failed: {exception.Message}",
                    rawResponse,
                    default);
            }
        }

        private string BuildUrl(string relativePath)
        {
            if (string.IsNullOrEmpty(_settings.BaseUrl))
            {
                return relativePath ?? string.Empty;
            }

            if (string.IsNullOrEmpty(relativePath))
            {
                return _settings.BaseUrl.TrimEnd('/');
            }

            return $"{_settings.BaseUrl.TrimEnd('/')}/{relativePath.TrimStart('/')}";
        }

        private void ApplyHeaders(UnityWebRequest request)
        {
            if (_settings.DefaultHeaders == null)
            {
                return;
            }

            foreach (var header in _settings.DefaultHeaders)
            {
                if (header == null || string.IsNullOrWhiteSpace(header.Key))
                {
                    continue;
                }

                request.SetRequestHeader(header.Key, header.Value ?? string.Empty);
            }
        }
    }
}
