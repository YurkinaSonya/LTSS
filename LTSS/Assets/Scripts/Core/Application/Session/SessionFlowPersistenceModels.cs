using System;

namespace Game.Core.Application.Session
{
    [Serializable]
    public sealed class PersistedSessionFlowProgressSnapshot
    {
        public string runId;
        public int sessionConfigVersion;
        public int schemaVersion;
        public string[] completedPreSessionStepKeys;
        public string[] completedPeriodContentBlockKeys;
        public string[] completedPostPeriodSurveyKeys;
        public string[] completedInterPeriodBlockKeys;
        public string[] completedPostSessionStepKeys;
        public int[] completedPeriodNumbers;
        public int activePeriodNumber;
        public string activeStepKey;
        public string activeStepScope;
        public string activeStepType;
        public string activeStepTitle;
        public bool activeStepRequired;
        public bool isPostSessionCompleted;
        public bool isSessionCompleted;
        public string savedAtUtc;
        public bool canRestore;

        public SessionFlowProgressState ToProgressState()
        {
            return new SessionFlowProgressState(
                completedPreSessionStepKeys ?? Array.Empty<string>(),
                completedPeriodContentBlockKeys ?? Array.Empty<string>(),
                completedPostPeriodSurveyKeys ?? Array.Empty<string>(),
                completedInterPeriodBlockKeys ?? Array.Empty<string>(),
                completedPostSessionStepKeys ?? Array.Empty<string>(),
                completedPeriodNumbers ?? Array.Empty<int>(),
                activePeriodNumber,
                new SessionFlowStepDescriptor(
                    activeStepKey,
                    ToScope(activeStepScope),
                    activePeriodNumber,
                    ToStepType(activeStepType),
                    activeStepTitle,
                    activeStepRequired),
                isPostSessionCompleted,
                isSessionCompleted);
        }

        private static SessionFlowStepScope ToScope(string rawValue)
        {
            switch ((rawValue ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "presession":
                    return SessionFlowStepScope.PreSession;
                case "periodcontent":
                    return SessionFlowStepScope.PeriodContent;
                case "periodgameplay":
                    return SessionFlowStepScope.PeriodGameplay;
                case "postperiodsurvey":
                    return SessionFlowStepScope.PostPeriodSurvey;
                case "interperiodblock":
                    return SessionFlowStepScope.InterPeriodBlock;
                case "postsession":
                    return SessionFlowStepScope.PostSession;
                case "completion":
                    return SessionFlowStepScope.Completion;
                default:
                    return SessionFlowStepScope.None;
            }
        }

        private static SessionFlowStepType ToStepType(string rawValue)
        {
            switch ((rawValue ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "instruction":
                    return SessionFlowStepType.Instruction;
                case "instructionquiz":
                    return SessionFlowStepType.InstructionQuiz;
                case "pretest":
                    return SessionFlowStepType.PreTest;
                case "posttest":
                    return SessionFlowStepType.PostTest;
                case "postperiodsurvey":
                    return SessionFlowStepType.PostPeriodSurvey;
                case "instructionalpopup":
                    return SessionFlowStepType.InstructionalPopup;
                case "news":
                    return SessionFlowStepType.News;
                default:
                    return SessionFlowStepType.Unknown;
            }
        }
    }
}
