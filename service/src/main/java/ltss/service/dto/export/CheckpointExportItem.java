package ltss.service.dto.export;

import java.time.LocalDateTime;

public record CheckpointExportItem(
        Long checkpointId,
        String runId,
        Long participantAccountId,
        String participantLogin,
        Integer periodNumber,
        LocalDateTime submittedAt,
        String summaryJson,
        String checkpointJson
) {
}
