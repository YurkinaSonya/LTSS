package ltss.service.dto.export;

import java.util.List;

public record SessionSummaryExportResponse(
        Long sessionDefinitionId,
        String sessionDefinitionCode,
        String sessionTitle,
        long accountCount,
        long runCount,
        long completedRunCount,
        long checkpointCount,
        long surveySubmissionCount,
        long logBatchCount,
        List<SessionParticipantSummaryItem> participants
) {
}
