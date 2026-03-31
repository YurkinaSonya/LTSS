package ltss.service.dto.admin;

import java.time.LocalDateTime;
import ltss.service.enums.ParticipantRunStatus;

public record ParticipantRunResponse(
        String id,
        Long participantAccountId,
        String participantLogin,
        String sessionDefinitionCode,
        ParticipantRunStatus runStatus,
        Integer currentPeriodNumber,
        Integer bootstrapVersion,
        LocalDateTime startedAt,
        LocalDateTime finishedAt,
        LocalDateTime lastCheckpointAt,
        LocalDateTime createdAt,
        LocalDateTime updatedAt
) {
}
