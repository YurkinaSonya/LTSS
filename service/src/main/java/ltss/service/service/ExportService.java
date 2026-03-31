package ltss.service.service;

import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import lombok.RequiredArgsConstructor;
import ltss.service.dto.export.AccountRunExportItem;
import ltss.service.dto.export.AccountRunExportResponse;
import ltss.service.dto.export.CheckpointExportResponse;
import ltss.service.dto.export.SessionParticipantSummaryItem;
import ltss.service.dto.export.SessionSummaryExportResponse;
import ltss.service.dto.export.SurveyExportResponse;
import ltss.service.entity.ExperimentSessionDefinition;
import ltss.service.entity.ParticipantAccount;
import ltss.service.entity.ParticipantRun;
import ltss.service.enums.ParticipantRunStatus;
import ltss.service.exception.NotFoundException;
import ltss.service.mapper.ExportMapper;
import ltss.service.repository.EventLogBatchRepository;
import ltss.service.repository.ExperimentSessionDefinitionRepository;
import ltss.service.repository.ParticipantAccountRepository;
import ltss.service.repository.ParticipantRunRepository;
import ltss.service.repository.PeriodCheckpointRepository;
import ltss.service.repository.SurveySubmissionRepository;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

@Service
@RequiredArgsConstructor
public class ExportService {

    private final ExperimentSessionDefinitionRepository sessionDefinitionRepository;
    private final ParticipantAccountRepository participantAccountRepository;
    private final ParticipantRunRepository participantRunRepository;
    private final PeriodCheckpointRepository periodCheckpointRepository;
    private final SurveySubmissionRepository surveySubmissionRepository;
    private final EventLogBatchRepository eventLogBatchRepository;
    private final ExportMapper exportMapper;

    @Transactional(readOnly = true)
    public SessionSummaryExportResponse exportSummary(Long sessionDefinitionId) {
        ExperimentSessionDefinition sessionDefinition = getSessionDefinition(sessionDefinitionId);
        List<ParticipantAccount> accounts = participantAccountRepository.findBySessionDefinitionIdOrderByIdAsc(sessionDefinitionId);
        List<ParticipantRun> runs = participantRunRepository
                .findByParticipantAccount_SessionDefinition_IdOrderByCreatedAtDesc(sessionDefinitionId);

        Map<Long, ParticipantRun> latestRunByAccountId = new LinkedHashMap<>();
        for (ParticipantRun run : runs) {
            latestRunByAccountId.putIfAbsent(run.getParticipantAccount().getId(), run);
        }

        List<SessionParticipantSummaryItem> participants = accounts.stream()
                .map(account -> exportMapper.toSessionParticipantSummaryItem(account, latestRunByAccountId.get(account.getId())))
                .toList();

        return new SessionSummaryExportResponse(
                sessionDefinition.getId(),
                sessionDefinition.getCode(),
                sessionDefinition.getTitle(),
                accounts.size(),
                runs.size(),
                runs.stream().filter(run -> run.getRunStatus() == ParticipantRunStatus.COMPLETED).count(),
                periodCheckpointRepository.findAllBySessionDefinitionId(sessionDefinitionId).size(),
                surveySubmissionRepository.findAllBySessionDefinitionId(sessionDefinitionId).size(),
                eventLogBatchRepository.findAllBySessionDefinitionId(sessionDefinitionId).size(),
                participants
        );
    }

    @Transactional(readOnly = true)
    public CheckpointExportResponse exportCheckpoints(Long sessionDefinitionId) {
        ExperimentSessionDefinition sessionDefinition = getSessionDefinition(sessionDefinitionId);
        return new CheckpointExportResponse(
                sessionDefinition.getId(),
                sessionDefinition.getCode(),
                periodCheckpointRepository.findAllBySessionDefinitionId(sessionDefinitionId)
                        .stream()
                        .map(exportMapper::toCheckpointExportItem)
                        .toList()
        );
    }

    @Transactional(readOnly = true)
    public SurveyExportResponse exportSurveys(Long sessionDefinitionId) {
        ExperimentSessionDefinition sessionDefinition = getSessionDefinition(sessionDefinitionId);
        return new SurveyExportResponse(
                sessionDefinition.getId(),
                sessionDefinition.getCode(),
                surveySubmissionRepository.findAllBySessionDefinitionId(sessionDefinitionId)
                        .stream()
                        .map(exportMapper::toSurveyExportItem)
                        .toList()
        );
    }

    @Transactional(readOnly = true)
    public AccountRunExportResponse exportAccountsRuns(Long sessionDefinitionId) {
        ExperimentSessionDefinition sessionDefinition = getSessionDefinition(sessionDefinitionId);
        List<ParticipantAccount> accounts = participantAccountRepository.findBySessionDefinitionIdOrderByIdAsc(sessionDefinitionId);
        List<ParticipantRun> runs = participantRunRepository
                .findByParticipantAccount_SessionDefinition_IdOrderByCreatedAtDesc(sessionDefinitionId);

        Map<Long, ParticipantRun> latestRunByAccountId = new LinkedHashMap<>();
        for (ParticipantRun run : runs) {
            latestRunByAccountId.putIfAbsent(run.getParticipantAccount().getId(), run);
        }

        List<AccountRunExportItem> items = accounts.stream()
                .map(account -> exportMapper.toAccountRunExportItem(account, latestRunByAccountId.get(account.getId())))
                .toList();

        return new AccountRunExportResponse(sessionDefinition.getId(), sessionDefinition.getCode(), items);
    }

    private ExperimentSessionDefinition getSessionDefinition(Long id) {
        return sessionDefinitionRepository.findById(id)
                .orElseThrow(() -> new NotFoundException("Session definition not found: " + id));
    }
}
