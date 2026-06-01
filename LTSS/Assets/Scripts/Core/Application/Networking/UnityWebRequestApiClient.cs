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
        private const int MaxLoggedBodyLength = 400;

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
            Action<ApiResponse<TResponse>> onCompleted = null,
            ApiRequestOptions options = null)
        {
            if (_coroutineRunner == null)
            {
                _logger.Error("CoroutineRunner is not available for HTTP GET request execution.");
                onCompleted?.Invoke(new ApiResponse<TResponse>(
                    false,
                    0,
                    "CoroutineRunner is not available.",
                    string.Empty,
                    default));
                return;
            }

            _coroutineRunner.StartCoroutine(
                SendRequest(relativePath, UnityWebRequest.kHttpVerbGET, null, onCompleted, options));
        }

        public void Post<TRequest, TResponse>(
            string relativePath,
            TRequest body,
            Action<ApiResponse<TResponse>> onCompleted = null,
            ApiRequestOptions options = null)
        {
            if (_coroutineRunner == null)
            {
                _logger.Error("CoroutineRunner is not available for HTTP POST request execution.");
                onCompleted?.Invoke(new ApiResponse<TResponse>(
                    false,
                    0,
                    "CoroutineRunner is not available.",
                    string.Empty,
                    default));
                return;
            }

            var payload = body == null ? string.Empty : _serializer.Serialize(body);

            _coroutineRunner.StartCoroutine(
                SendRequest(relativePath, UnityWebRequest.kHttpVerbPOST, payload, onCompleted, options));
        }

        public void PostRaw<TResponse>(
            string relativePath,
            string jsonPayload,
            Action<ApiResponse<TResponse>> onCompleted = null,
            ApiRequestOptions options = null)
        {
            if (_coroutineRunner == null)
            {
                _logger.Error("CoroutineRunner is not available for HTTP POST request execution.");
                onCompleted?.Invoke(new ApiResponse<TResponse>(
                    false,
                    0,
                    "CoroutineRunner is not available.",
                    string.Empty,
                    default));
                return;
            }

            _coroutineRunner.StartCoroutine(
                SendRequest(relativePath, UnityWebRequest.kHttpVerbPOST, jsonPayload ?? string.Empty, onCompleted, options));
        }

        private IEnumerator SendRequest<TResponse>(
            string relativePath,
            string method,
            string payload,
            Action<ApiResponse<TResponse>> onCompleted,
            ApiRequestOptions options)
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

                ApplyHeaders(request, options);

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
                    BuildErrorDetail(request.error, rawResponse),
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

                if (!isSuccess)
                {
                    metadata["error"] = response.Error;

                    var responseSnippet = BuildResponseSnippet(rawResponse);

                    if (!string.IsNullOrWhiteSpace(responseSnippet))
                    {
                        metadata["response"] = responseSnippet;
                    }
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
                    response.Error));

                if (!isSuccess)
                {
                    var responseSnippet = BuildResponseSnippet(rawResponse);
                    var logMessage =
                        $"HTTP request failed. Method: {method}, Url: {requestUrl}, " +
                        $"Status: {request.responseCode}, Error: {response.Error}";

                    if (!string.IsNullOrWhiteSpace(responseSnippet))
                    {
                        logMessage += $", Response: {responseSnippet}";
                    }

                    _logger.Warning(logMessage);
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

        private void ApplyHeaders(UnityWebRequest request, ApiRequestOptions options)
        {
            if (_settings.DefaultHeaders == null)
            {
                if (!string.IsNullOrWhiteSpace(options?.BearerToken))
                {
                    request.SetRequestHeader("Authorization", $"Bearer {options.BearerToken}");
                }
            }
            else
            {
                foreach (var header in _settings.DefaultHeaders)
                {
                    if (header == null || string.IsNullOrWhiteSpace(header.Key))
                    {
                        continue;
                    }

                    request.SetRequestHeader(header.Key, header.Value ?? string.Empty);
                }
            }

            if (!string.IsNullOrWhiteSpace(options?.BearerToken))
            {
                request.SetRequestHeader("Authorization", $"Bearer {options.BearerToken}");
            }

            if (options?.Headers == null)
            {
                return;
            }

            foreach (var header in options.Headers)
            {
                if (string.IsNullOrWhiteSpace(header.Key))
                {
                    continue;
                }

                request.SetRequestHeader(header.Key, header.Value ?? string.Empty);
            }
        }

        private static string BuildErrorDetail(string requestError, string rawResponse)
        {
            var responseError = TryExtractErrorFromResponse(rawResponse);

            if (!string.IsNullOrWhiteSpace(responseError))
            {
                return responseError;
            }

            return string.IsNullOrWhiteSpace(requestError)
                ? "Запрос завершился с ошибкой."
                : requestError.Trim();
        }

        private static string TryExtractErrorFromResponse(string rawResponse)
        {
            if (string.IsNullOrWhiteSpace(rawResponse))
            {
                return string.Empty;
            }

            var trimmed = rawResponse.Trim();
            var jsonMessage = TryExtractJsonValue(trimmed, "message");

            if (string.IsNullOrWhiteSpace(jsonMessage))
            {
                jsonMessage = TryExtractJsonValue(trimmed, "error");
            }

            if (string.IsNullOrWhiteSpace(jsonMessage))
            {
                jsonMessage = TryExtractJsonValue(trimmed, "detail");
            }

            if (string.IsNullOrWhiteSpace(jsonMessage))
            {
                jsonMessage = TryExtractJsonValue(trimmed, "title");
            }

            return !string.IsNullOrWhiteSpace(jsonMessage)
                ? jsonMessage
                : BuildResponseSnippet(trimmed);
        }

        private static string TryExtractJsonValue(string rawResponse, string propertyName)
        {
            if (string.IsNullOrWhiteSpace(rawResponse) || string.IsNullOrWhiteSpace(propertyName))
            {
                return string.Empty;
            }

            var pattern = $"\"{propertyName}\"";
            var propertyIndex = rawResponse.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);

            if (propertyIndex < 0)
            {
                return string.Empty;
            }

            var colonIndex = rawResponse.IndexOf(':', propertyIndex + pattern.Length);

            if (colonIndex < 0)
            {
                return string.Empty;
            }

            var valueStart = colonIndex + 1;

            while (valueStart < rawResponse.Length && char.IsWhiteSpace(rawResponse[valueStart]))
            {
                valueStart++;
            }

            if (valueStart >= rawResponse.Length)
            {
                return string.Empty;
            }

            if (rawResponse[valueStart] == '"')
            {
                valueStart++;
                var builder = new StringBuilder();
                var isEscaped = false;

                while (valueStart < rawResponse.Length)
                {
                    var symbol = rawResponse[valueStart++];

                    if (isEscaped)
                    {
                        switch (symbol)
                        {
                            case '"':
                            case '\\':
                            case '/':
                                builder.Append(symbol);
                                break;
                            case 'n':
                                builder.Append(' ');
                                break;
                            case 'r':
                            case 't':
                                builder.Append(' ');
                                break;
                            default:
                                builder.Append(symbol);
                                break;
                        }

                        isEscaped = false;
                        continue;
                    }

                    if (symbol == '\\')
                    {
                        isEscaped = true;
                        continue;
                    }

                    if (symbol == '"')
                    {
                        break;
                    }

                    builder.Append(symbol);
                }

                return NormalizeWhitespace(builder.ToString());
            }

            var valueEnd = valueStart;

            while (valueEnd < rawResponse.Length
                   && rawResponse[valueEnd] != ','
                   && rawResponse[valueEnd] != '}'
                   && rawResponse[valueEnd] != ']')
            {
                valueEnd++;
            }

            return NormalizeWhitespace(rawResponse.Substring(valueStart, valueEnd - valueStart));
        }

        private static string BuildResponseSnippet(string rawResponse)
        {
            var compact = NormalizeWhitespace(rawResponse);

            if (string.IsNullOrWhiteSpace(compact))
            {
                return string.Empty;
            }

            if (compact.Length > MaxLoggedBodyLength)
            {
                return compact.Substring(0, MaxLoggedBodyLength) + "...";
            }

            return compact;
        }

        private static string NormalizeWhitespace(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var compact = value
                .Replace('\r', ' ')
                .Replace('\n', ' ')
                .Replace('\t', ' ')
                .Trim();

            while (compact.Contains("  "))
            {
                compact = compact.Replace("  ", " ");
            }

            return compact;
        }
    }
}
