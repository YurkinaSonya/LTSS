package ltss.service.dto.run;

import java.util.List;

public record BootstrapResponse(
        BootstrapRunResponse run,
        BootstrapSessionResponse session,
        BootstrapParticipantResponse participant,
        BootstrapStatisticalDatasetResponse statisticalDataset,
        List<BootstrapSurveyTemplateResponse> surveyTemplates
) {
}