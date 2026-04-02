package ltss.service.dto.admin;

import java.time.LocalDateTime;
import ltss.service.enums.ExperimentSessionStatus;

public record SessionDefinitionResponse(
        Long id,
        String code,
        String title,
        String description,
        ExperimentSessionStatus status,
        Integer configVersion,
        Integer participantCountPlanned,
        Long statisticalDatasetId,
        String sessionConfigJson,
        LocalDateTime createdAt,
        LocalDateTime updatedAt
) {
}