package ltss.service.dto.run;

import jakarta.validation.constraints.Min;
import jakarta.validation.constraints.NotBlank;

public record EventLogBatchRequest(
        @Min(value = 1, message = "periodNumber must be greater than 0 when provided")
        Integer periodNumber,
        @NotBlank(message = "batchType is required")
        String batchType,
        @NotBlank(message = "payloadJson is required")
        String payloadJson
) {
}
