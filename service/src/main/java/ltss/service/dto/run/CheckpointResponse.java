package ltss.service.dto.run;

import java.time.LocalDateTime;

public record CheckpointResponse(
        Long checkpointId,
        String runId,
        Integer currentPeriodNumber,
        LocalDateTime submittedAt
) {
}
