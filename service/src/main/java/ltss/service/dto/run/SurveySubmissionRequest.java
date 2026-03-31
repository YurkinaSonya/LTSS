package ltss.service.dto.run;

import jakarta.validation.constraints.Min;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;
import java.time.LocalDateTime;

public record SurveySubmissionRequest(
        @NotNull(message = "surveyTemplateId is required")
        Long surveyTemplateId,
        @Min(value = 1, message = "periodNumber must be greater than 0 when provided")
        Integer periodNumber,
        @NotBlank(message = "responseJson is required")
        String responseJson,
        LocalDateTime submittedAt
) {
}
