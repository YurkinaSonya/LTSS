package ltss.service.dto.export;

import java.util.List;

public record SurveyExportResponse(
        Long sessionDefinitionId,
        String sessionDefinitionCode,
        List<SurveyExportItem> items
) {
}
