package ltss.service.dto.admin;

import jakarta.validation.constraints.Min;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;
import ltss.service.enums.SurveyType;

public record SurveyTemplateRequest(
        @NotNull(message = "sessionDefinitionId is required")
        Long sessionDefinitionId,
        @NotBlank(message = "code is required")
        String code,
        @NotBlank(message = "title is required")
        String title,
        @NotNull(message = "type is required")
        SurveyType type,
        @NotNull(message = "version is required")
        @Min(value = 1, message = "version must be greater than 0")
        Integer version,
        @NotBlank(message = "templateJson is required")
        String templateJson,
        @NotNull(message = "enabled is required")
        Boolean enabled
) {
}
