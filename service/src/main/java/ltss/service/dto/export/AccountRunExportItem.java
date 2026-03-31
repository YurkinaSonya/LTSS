package ltss.service.dto.export;

import java.time.LocalDateTime;
import ltss.service.enums.ParticipantAccountStatus;
import ltss.service.enums.ParticipantRunStatus;

public record AccountRunExportItem(
        Long participantAccountId,
        String login,
        ParticipantAccountStatus accountStatus,
        String assignedGroupCode,
        String runId,
        ParticipantRunStatus runStatus,
        Integer currentPeriodNumber,
        Integer bootstrapVersion,
        LocalDateTime startedAt,
        LocalDateTime finishedAt,
        LocalDateTime lastCheckpointAt,
        LocalDateTime accountCreatedAt,
        LocalDateTime runCreatedAt
) {
}
