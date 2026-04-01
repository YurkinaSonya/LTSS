using System;
using System.Collections.Generic;
using Game.Core.Application.Networking;

namespace Game.Core.Application.Session
{
    [Serializable]
    public sealed class CheckpointRequestDto
    {
        public int periodNumber;
        public string payloadJson;
    }

    [Serializable]
    public sealed class SurveySubmissionRequestDto
    {
        public string surveyCode;
        public string payloadJson;
    }

    [Serializable]
    public sealed class LogBatchRequestDto
    {
        public string[] entries;
    }

    [Serializable]
    public sealed class CompleteRunRequestDto
    {
        public string reason;
    }

    [Serializable]
    public sealed class ServerAckDto
    {
        public string status;
        public string message;
    }

    public sealed class LocalOutboxItem
    {
        public string Id { get; }
        public string RunId { get; }
        public string Kind { get; }
        public string PayloadJson { get; }

        public LocalOutboxItem(
            string id,
            string runId,
            string kind,
            string payloadJson)
        {
            Id = id ?? Guid.NewGuid().ToString("N");
            RunId = runId ?? string.Empty;
            Kind = kind ?? string.Empty;
            PayloadJson = payloadJson ?? string.Empty;
        }
    }

    public interface ICheckpointSender
    {
        void Send(
            string runId,
            string bearerToken,
            CheckpointRequestDto request,
            Action<ApiResponse<ServerAckDto>> onCompleted = null);
    }

    public interface ISurveySubmissionSender
    {
        void Send(
            string runId,
            string bearerToken,
            SurveySubmissionRequestDto request,
            Action<ApiResponse<ServerAckDto>> onCompleted = null);
    }

    public interface ILogBatchSender
    {
        void Send(
            string runId,
            string bearerToken,
            LogBatchRequestDto request,
            Action<ApiResponse<ServerAckDto>> onCompleted = null);
    }

    public interface ICompleteRunSender
    {
        void Send(
            string runId,
            string bearerToken,
            CompleteRunRequestDto request,
            Action<ApiResponse<ServerAckDto>> onCompleted = null);
    }

    public interface ILocalOutboxQueue
    {
        void Enqueue(LocalOutboxItem item);
        IReadOnlyList<LocalOutboxItem> GetPending();
        void Remove(string itemId);
        void Clear();
    }
}
