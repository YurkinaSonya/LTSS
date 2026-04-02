package ltss.service.mapper;

import ltss.service.dto.admin.ParticipantAccountResponse;
import ltss.service.dto.admin.ParticipantRunResponse;
import ltss.service.dto.run.BootstrapParticipantResponse;
import ltss.service.dto.run.BootstrapRunResponse;
import ltss.service.dto.run.CurrentRunResponse;
import ltss.service.entity.ParticipantAccount;
import ltss.service.entity.ParticipantRun;
import org.springframework.stereotype.Component;

@Component
public class ParticipantMapper {

    public CurrentRunResponse toCurrentRunResponse(ParticipantRun entity) {
        ParticipantAccount account = entity.getParticipantAccount();
        return new CurrentRunResponse(
                entity.getId(),
                account.getSessionDefinition().getCode(),
                entity.getRunStatus(),
                entity.getCurrentPeriodNumber(),
                account.getAssignedGroupCode()
        );
    }

    public BootstrapRunResponse toBootstrapRunResponse(ParticipantRun entity) {
        return new BootstrapRunResponse(
                entity.getId(),
                entity.getRunStatus(),
                entity.getCurrentPeriodNumber(),
                entity.getBootstrapVersion(),
                entity.getStartedAt(),
                entity.getFinishedAt(),
                entity.getLastCheckpointAt()
        );
    }

    public BootstrapParticipantResponse toBootstrapParticipantResponse(ParticipantAccount entity) {
        return new BootstrapParticipantResponse(
                entity.getId(),
                entity.getLogin(),
                entity.getAssignedGroupCode(),
                entity.getAssignedConfigJson(),
                entity.getDeviceBindingJson()
        );
    }

    public ParticipantAccountResponse toParticipantAccountResponse(ParticipantAccount entity) {
        return new ParticipantAccountResponse(
                entity.getId(),
                entity.getSessionDefinition().getId(),
                entity.getLogin(),
                entity.getStatus(),
                entity.getAssignedGroupCode(),
                entity.getAssignedConfigJson(),
                entity.getDeviceBindingJson(),
                entity.getCreatedAt(),
                entity.getUpdatedAt()
        );
    }

    public ParticipantRunResponse toParticipantRunResponse(ParticipantRun entity) {
        ParticipantAccount account = entity.getParticipantAccount();
        return new ParticipantRunResponse(
                entity.getId(),
                account.getId(),
                account.getLogin(),
                account.getAssignedGroupCode(),
                account.getSessionDefinition().getCode(),
                entity.getRunStatus(),
                entity.getCurrentPeriodNumber(),
                entity.getBootstrapVersion(),
                entity.getStartedAt(),
                entity.getFinishedAt(),
                entity.getLastCheckpointAt(),
                entity.getCreatedAt(),
                entity.getUpdatedAt()
        );
    }
}
