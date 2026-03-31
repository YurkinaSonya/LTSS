package ltss.service.dto.run;

import ltss.service.enums.SurveyType;

public record BootstrapSurveyTemplateResponse(
        Long id,
        String code,
        String title,
        SurveyType type,
        Integer version,
        String templateJson
) {
}
