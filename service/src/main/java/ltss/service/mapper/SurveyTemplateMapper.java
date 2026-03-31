package ltss.service.mapper;

import ltss.service.dto.admin.SurveyTemplateRequest;
import ltss.service.dto.admin.SurveyTemplateResponse;
import ltss.service.dto.run.BootstrapSurveyTemplateResponse;
import ltss.service.entity.SurveyTemplate;
import org.springframework.stereotype.Component;

@Component
public class SurveyTemplateMapper {

    public SurveyTemplateResponse toResponse(SurveyTemplate entity) {
        return new SurveyTemplateResponse(
                entity.getId(),
                entity.getSessionDefinition().getId(),
                entity.getSessionDefinition().getCode(),
                entity.getCode(),
                entity.getTitle(),
                entity.getType(),
                entity.getVersion(),
                entity.getTemplateJson(),
                entity.isEnabled(),
                entity.getCreatedAt(),
                entity.getUpdatedAt()
        );
    }

    public BootstrapSurveyTemplateResponse toBootstrapResponse(SurveyTemplate entity) {
        return new BootstrapSurveyTemplateResponse(
                entity.getId(),
                entity.getCode(),
                entity.getTitle(),
                entity.getType(),
                entity.getVersion(),
                entity.getTemplateJson()
        );
    }

    public void updateEntity(SurveyTemplate entity, SurveyTemplateRequest request) {
        entity.setCode(request.code());
        entity.setTitle(request.title());
        entity.setType(request.type());
        entity.setVersion(request.version());
        entity.setTemplateJson(request.templateJson());
        entity.setEnabled(Boolean.TRUE.equals(request.enabled()));
    }
}
