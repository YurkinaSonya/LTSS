using System;

namespace Game.Core.Application.Session
{
    [Serializable]
    public sealed class LoginRequestDto
    {
        public string login;
        public string password;

        public LoginRequestDto(string loginValue, string passwordValue)
        {
            login = loginValue ?? string.Empty;
            password = passwordValue ?? string.Empty;
        }
    }

    [Serializable]
    public sealed class LoginResponseDto
    {
        public string token;
        public RunInfoDto run;
    }

    [Serializable]
    public sealed class RunCurrentEnvelopeDto
    {
        public RunInfoDto run;
    }

    [Serializable]
    public sealed class RunInfoDto
    {
        public string runId;
        public string sessionDefinitionCode;
        public string runStatus;
        public int currentPeriodNumber;
        public string assignedGroupCode;
    }

    [Serializable]
    public sealed class BootstrapResponseDto
    {
        public BootstrapRunDto run;
        public SessionDefinitionDto session;
        public ParticipantDto participant;
        public SurveyTemplateDto[] surveyTemplates;
    }

    [Serializable]
    public sealed class BootstrapRunDto
    {
        public string runId;
        public string runStatus;
        public int currentPeriodNumber;
        public int bootstrapVersion;
        public string startedAt;
        public string finishedAt;
        public string lastCheckpointAt;
    }

    [Serializable]
    public sealed class SessionDefinitionDto
    {
        public int sessionDefinitionId;
        public string code;
        public string title;
        public string description;
        public string status;
        public int configVersion;
        public int participantCountPlanned;
        public string sessionConfigJson;
    }

    [Serializable]
    public sealed class ParticipantDto
    {
        public int participantAccountId;
        public string login;
        public string assignedGroupCode;
        public string assignedConfigJson;
        public string deviceBindingJson;
    }

    [Serializable]
    public sealed class SurveyTemplateDto
    {
        public int id;
        public string code;
        public string title;
        public string type;
        public int version;
        public string templateJson;
    }
}
