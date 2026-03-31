package ltss.service.mapper;

import ltss.service.dto.export.AccountRunExportItem;
import ltss.service.dto.export.CheckpointExportItem;
import ltss.service.dto.export.SessionParticipantSummaryItem;
import ltss.service.dto.export.SurveyExportItem;
import ltss.service.entity.ParticipantAccount;
import ltss.service.entity.ParticipantRun;
import ltss.service.entity.PeriodCheckpoint;
import ltss.service.entity.SurveySubmission;
import org.springframework.stereotype.Component;

@Component
public class ExportMapper {

    public SessionParticipantSummaryItem toSessionParticipantSummaryItem(ParticipantAccount account, ParticipantRun run) {
        return new SessionParticipantSummaryItem(
                account.getId(),
                account.getLogin(),
                account.getStatus(),
                account.getAssignedGroupCode(),
                run != null ? run.getId() : null,
                run != null ? run.getRunStatus() : null,
                run != null ? run.getCurrentPeriodNumber() : null,
                run != null ? run.getStartedAt() : null,
                run != null ? run.getFinishedAt() : null,
                run != null ? run.getLastCheckpointAt() : null
        );
    }

    public CheckpointExportItem toCheckpointExportItem(PeriodCheckpoint entity) {
        ParticipantAccount account = entity.getParticipantRun().getParticipantAccount();
        return new CheckpointExportItem(
                entity.getId(),
                entity.getParticipantRun().getId(),
                account.getId(),
                account.getLogin(),
                entity.getPeriodNumber(),
                entity.getSubmittedAt(),
                entity.getSummaryJson(),
                entity.getCheckpointJson()
        );
    }

    public SurveyExportItem toSurveyExportItem(SurveySubmission entity) {
        ParticipantAccount account = entity.getParticipantRun().getParticipantAccount();
        return new SurveyExportItem(
                entity.getId(),
                entity.getParticipantRun().getId(),
                account.getId(),
                account.getLogin(),
                entity.getSurveyTemplate().getId(),
                entity.getSurveyTemplate().getCode(),
                entity.getPeriodNumber(),
                entity.getSubmittedAt(),
                entity.getResponseJson()
        );
    }

    public AccountRunExportItem toAccountRunExportItem(ParticipantAccount account, ParticipantRun run) {
        return new AccountRunExportItem(
                account.getId(),
                account.getLogin(),
                account.getStatus(),
                account.getAssignedGroupCode(),
                run != null ? run.getId() : null,
                run != null ? run.getRunStatus() : null,
                run != null ? run.getCurrentPeriodNumber() : null,
                run != null ? run.getBootstrapVersion() : null,
                run != null ? run.getStartedAt() : null,
                run != null ? run.getFinishedAt() : null,
                run != null ? run.getLastCheckpointAt() : null,
                account.getCreatedAt(),
                run != null ? run.getCreatedAt() : null
        );
    }
}
