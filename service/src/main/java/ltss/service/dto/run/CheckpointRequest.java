package ltss.service.dto.run;

import jakarta.validation.constraints.Min;
import jakarta.validation.constraints.NotBlank;
import java.time.LocalDateTime;

public record CheckpointRequest(
        @Min(value = 1, message = "periodNumber must be greater than 0")
        Integer periodNumber,
        @NotBlank(message = "checkpointJson is required")
        String checkpointJson,
        String summaryJson,
        LocalDateTime submittedAt
) {
}
