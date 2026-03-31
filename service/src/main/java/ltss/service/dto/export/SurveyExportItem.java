package ltss.service.dto.export;

import java.time.LocalDateTime;

public record SurveyExportItem(
        Long submissionId,
        String runId,
        Long participantAccountId,
        String participantLogin,
        Long surveyTemplateId,
        String surveyCode,
        Integer periodNumber,
        LocalDateTime submittedAt,
        String responseJson
) {
}
