package ltss.service.dto.admin;

import java.time.LocalDateTime;
import ltss.service.enums.StatisticalDatasetStatus;

public record StatisticalDatasetResponse(
        Long id,
        String code,
        String title,
        String description,
        StatisticalDatasetStatus status,
        Integer version,
        String datasetJson,
        LocalDateTime createdAt,
        LocalDateTime updatedAt
) {
}