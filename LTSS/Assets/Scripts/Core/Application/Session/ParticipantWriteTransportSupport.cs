using System;
using System.Text;
using Game.Core.Application.Networking;

namespace Game.Core.Application.Session
{
    internal static class ParticipantWritePayloadBuilder
    {
        public static string BuildCheckpointRequestJson(CheckpointRequestDto request)
        {
            var builder = new StringBuilder(256);
            builder.Append('{');
            AppendIntProperty(builder, "periodNumber", request != null ? request.periodNumber : 0, true);
            AppendStringProperty(builder, "checkpointJson", request != null ? request.checkpointJson : string.Empty, false);
            AppendStringProperty(builder, "summaryJson", request != null ? request.summaryJson : string.Empty, false);
            AppendStringProperty(builder, "submittedAt", request != null ? request.submittedAt : string.Empty, false);
            builder.Append('}');
            return builder.ToString();
        }

        public static string BuildSurveySubmissionRequestJson(SurveySubmissionRequestDto request)
        {
            var builder = new StringBuilder(256);
            builder.Append('{');
            AppendIntProperty(builder, "surveyTemplateId", request != null ? request.surveyTemplateId : 0, true);
            AppendNullableIntProperty(
                builder,
                "periodNumber",
                request != null && request.hasPeriodNumber,
                request != null ? request.periodNumber : 0,
                false);
            AppendStringProperty(builder, "responseJson", request != null ? request.responseJson : string.Empty, false);
            AppendStringProperty(builder, "submittedAt", request != null ? request.submittedAt : string.Empty, false);
            builder.Append('}');
            return builder.ToString();
        }

        public static string BuildLogBatchRequestJson(LogBatchRequestDto request)
        {
            var builder = new StringBuilder(256);
            builder.Append('{');
            AppendNullableIntProperty(
                builder,
                "periodNumber",
                request != null && request.hasPeriodNumber,
                request != null ? request.periodNumber : 0,
                true);
            AppendStringProperty(builder, "batchType", request != null ? request.batchType : string.Empty, false);
            AppendStringProperty(builder, "payloadJson", request != null ? request.payloadJson : string.Empty, false);
            builder.Append('}');
            return builder.ToString();
        }

        private static void AppendIntProperty(StringBuilder builder, string name, int value, bool isFirst)
        {
            if (!isFirst)
            {
                builder.Append(',');
            }

            builder.Append('"');
            builder.Append(name);
            builder.Append("\":");
            builder.Append(value);
        }

        private static void AppendNullableIntProperty(
            StringBuilder builder,
            string name,
            bool hasValue,
            int value,
            bool isFirst)
        {
            if (!isFirst)
            {
                builder.Append(',');
            }

            builder.Append('"');
            builder.Append(name);
            builder.Append("\":");
            builder.Append(hasValue ? value.ToString() : "null");
        }

        private static void AppendStringProperty(StringBuilder builder, string name, string value, bool isFirst)
        {
            if (!isFirst)
            {
                builder.Append(',');
            }

            builder.Append('"');
            builder.Append(name);
            builder.Append("\":\"");
            builder.Append(EscapeJsonString(value));
            builder.Append('"');
        }

        private static string EscapeJsonString(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(value.Length + 16);

            for (var index = 0; index < value.Length; index++)
            {
                var symbol = value[index];

                switch (symbol)
                {
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '\b':
                        builder.Append("\\b");
                        break;
                    case '\f':
                        builder.Append("\\f");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        if (char.IsControl(symbol))
                        {
                            builder.Append("\\u");
                            builder.Append(((int)symbol).ToString("x4"));
                        }
                        else
                        {
                            builder.Append(symbol);
                        }

                        break;
                }
            }

            return builder.ToString();
        }
    }

    internal static class ParticipantWriteResponseParser
    {
        public static ApiResponse<ServerAckDto> ToAckResponse(
            ApiResponse<string> response,
            IJsonSerializer serializer)
        {
            if (response == null)
            {
                return new ApiResponse<ServerAckDto>(
                    false,
                    0,
                    "Empty transport response.",
                    string.Empty,
                    null);
            }

            if (!response.IsSuccess)
            {
                return new ApiResponse<ServerAckDto>(
                    false,
                    response.StatusCode,
                    response.Error,
                    response.RawResponse,
                    null);
            }

            if (string.IsNullOrWhiteSpace(response.Payload))
            {
                return new ApiResponse<ServerAckDto>(
                    true,
                    response.StatusCode,
                    string.Empty,
                    response.RawResponse,
                    new ServerAckDto
                    {
                        status = "ok",
                        message = string.Empty
                    });
            }

            if (!serializer.TryDeserialize(
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

            return new ApiResponse<ServerAckDto>(
                true,
                response.StatusCode,
                string.Empty,
                response.RawResponse,
                payload);
        }
    }
}
