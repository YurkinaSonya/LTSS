package ltss.service.dto.run;

import java.time.LocalDateTime;

public record SurveySubmissionResponse(
        Long submissionId,
        String runId,
        Long surveyTemplateId,
        Integer periodNumber,
        LocalDateTime submittedAt
) {
}
