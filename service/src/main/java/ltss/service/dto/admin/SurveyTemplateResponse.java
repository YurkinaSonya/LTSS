package ltss.service.dto.admin;

import java.time.LocalDateTime;
import ltss.service.enums.SurveyType;

public record SurveyTemplateResponse(
        Long id,
        Long sessionDefinitionId,
        String sessionDefinitionCode,
        String code,
        String title,
        SurveyType type,
        Integer version,
        String templateJson,
        boolean enabled,
        LocalDateTime createdAt,
        LocalDateTime updatedAt
) {
}
