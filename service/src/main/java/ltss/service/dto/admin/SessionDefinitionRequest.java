package ltss.service.dto.admin;

import jakarta.validation.constraints.Min;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;
import ltss.service.enums.ExperimentSessionStatus;

public record SessionDefinitionRequest(
        @NotBlank(message = "code is required")
        String code,
        @NotBlank(message = "title is required")
        String title,
        String description,
        @NotNull(message = "status is required")
        ExperimentSessionStatus status,
        @NotNull(message = "configVersion is required")
        @Min(value = 1, message = "configVersion must be greater than 0")
        Integer configVersion,
        @Min(value = 0, message = "participantCountPlanned must not be negative")
        Integer participantCountPlanned,
        Long statisticalDatasetId,
        @NotBlank(message = "sessionConfigJson is required")
        String sessionConfigJson
) {
}