package ltss.service.mapper;

import ltss.service.dto.admin.SessionDefinitionRequest;
import ltss.service.dto.admin.SessionDefinitionResponse;
import ltss.service.dto.run.BootstrapSessionResponse;
import ltss.service.entity.ExperimentSessionDefinition;
import org.springframework.stereotype.Component;

@Component
public class SessionDefinitionMapper {

    public SessionDefinitionResponse toResponse(ExperimentSessionDefinition entity) {
        return new SessionDefinitionResponse(
                entity.getId(),
                entity.getCode(),
                entity.getTitle(),
                entity.getDescription(),
                entity.getStatus(),
                entity.getConfigVersion(),
                entity.getParticipantCountPlanned(),
                entity.getStatisticalDataset() != null ? entity.getStatisticalDataset().getId() : null,
                entity.getSessionConfigJson(),
                entity.getCreatedAt(),
                entity.getUpdatedAt()
        );
    }

    public BootstrapSessionResponse toBootstrapResponse(ExperimentSessionDefinition entity) {
        return new BootstrapSessionResponse(
                entity.getId(),
                entity.getCode(),
                entity.getTitle(),
                entity.getDescription(),
                entity.getStatus(),
                entity.getConfigVersion(),
                entity.getParticipantCountPlanned(),
                entity.getStatisticalDataset() != null ? entity.getStatisticalDataset().getId() : null,
                entity.getSessionConfigJson()
        );
    }

    public void updateEntity(ExperimentSessionDefinition entity, SessionDefinitionRequest request) {
        entity.setCode(request.code());
        entity.setTitle(request.title());
        entity.setDescription(request.description());
        entity.setStatus(request.status());
        entity.setConfigVersion(request.configVersion());
        entity.setParticipantCountPlanned(request.participantCountPlanned());
        entity.setSessionConfigJson(request.sessionConfigJson());
    }
}