package ltss.service.dto.admin;

import jakarta.validation.constraints.Min;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;
import ltss.service.enums.StatisticalDatasetStatus;

public record StatisticalDatasetRequest(
        @NotBlank(message = "code is required")
        String code,
        @NotBlank(message = "title is required")
        String title,
        String description,
        @NotNull(message = "status is required")
        StatisticalDatasetStatus status,
        @NotNull(message = "version is required")
        @Min(value = 1, message = "version must be greater than 0")
        Integer version,
        @NotBlank(message = "datasetJson is required")
        String datasetJson
) {
}