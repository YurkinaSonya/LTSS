package ltss.service.dto.run;

import java.time.LocalDateTime;
import ltss.service.enums.ParticipantRunStatus;

public record BootstrapRunResponse(
        String runId,
        ParticipantRunStatus runStatus,
        Integer currentPeriodNumber,
        Integer bootstrapVersion,
        LocalDateTime startedAt,
        LocalDateTime finishedAt,
        LocalDateTime lastCheckpointAt
) {
}
