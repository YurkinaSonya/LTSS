package ltss.service.dto.run;

import ltss.service.enums.ParticipantRunStatus;

public record CurrentRunResponse(
        String runId,
        String sessionDefinitionCode,
        ParticipantRunStatus runStatus,
        Integer currentPeriodNumber,
        String assignedGroupCode
) {
}
