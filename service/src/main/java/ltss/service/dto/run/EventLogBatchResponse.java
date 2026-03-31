package ltss.service.dto.run;

import java.time.LocalDateTime;

public record EventLogBatchResponse(
        Long batchId,
        String runId,
        String batchType,
        LocalDateTime createdAt
) {
}
