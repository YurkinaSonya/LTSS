package ltss.service.service;

import java.time.LocalDateTime;
import lombok.RequiredArgsConstructor;
import ltss.service.dto.run.CheckpointRequest;
import ltss.service.dto.run.CheckpointResponse;
import ltss.service.dto.run.CompleteRunResponse;
import ltss.service.dto.run.CurrentRunResponse;
import ltss.service.entity.ParticipantAccount;
import ltss.service.entity.ParticipantRun;
import ltss.service.entity.PeriodCheckpoint;
import ltss.service.enums.ParticipantAccountStatus;
import ltss.service.enums.ParticipantRunStatus;
import ltss.service.exception.BadRequestException;
import ltss.service.mapper.ParticipantMapper;
import ltss.service.repository.ParticipantAccountRepository;
import ltss.service.repository.ParticipantRunRepository;
import ltss.service.repository.PeriodCheckpointRepository;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

@Service
@RequiredArgsConstructor
public class RunService {

    private final ParticipantMapper participantMapper;
    private final PeriodCheckpointRepository periodCheckpointRepository;
    private final ParticipantRunRepository participantRunRepository;
    private final ParticipantAccountRepository participantAccountRepository;

    @Transactional(readOnly = true)
    public CurrentRunResponse getCurrentRun(ParticipantRun run) {
        return participantMapper.toCurrentRunResponse(run);
    }

    @Transactional
    public CheckpointResponse submitCheckpoint(ParticipantRun run, CheckpointRequest request) {
        ensureRunIsWritable(run);

        LocalDateTime submittedAt = request.submittedAt() != null ? request.submittedAt() : LocalDateTime.now();

        PeriodCheckpoint checkpoint = new PeriodCheckpoint();
        checkpoint.setParticipantRun(run);
        checkpoint.setPeriodNumber(request.periodNumber());
        checkpoint.setCheckpointJson(request.checkpointJson());
        checkpoint.setSummaryJson(request.summaryJson());
        checkpoint.setSubmittedAt(submittedAt);

        PeriodCheckpoint savedCheckpoint = periodCheckpointRepository.save(checkpoint);
        advanceRun(run, request.periodNumber(), submittedAt);

        return new CheckpointResponse(
                savedCheckpoint.getId(),
                run.getId(),
                run.getCurrentPeriodNumber(),
                savedCheckpoint.getSubmittedAt()
        );
    }

    @Transactional
    public CompleteRunResponse completeRun(ParticipantRun run) {
        if (run.getRunStatus() == ParticipantRunStatus.ABORTED) {
            throw new BadRequestException("Aborted run cannot be completed");
        }
        if (run.getRunStatus() != ParticipantRunStatus.COMPLETED) {
            run.setRunStatus(ParticipantRunStatus.COMPLETED);
            if (run.getFinishedAt() == null) {
                run.setFinishedAt(LocalDateTime.now());
            }
            participantRunRepository.save(run);

            ParticipantAccount account = run.getParticipantAccount();
            account.setStatus(ParticipantAccountStatus.COMPLETED);
            participantAccountRepository.save(account);
        }

        return new CompleteRunResponse(run.getId(), run.getRunStatus(), run.getFinishedAt());
    }

    private void ensureRunIsWritable(ParticipantRun run) {
        if (run.getRunStatus() == ParticipantRunStatus.COMPLETED || run.getRunStatus() == ParticipantRunStatus.ABORTED) {
            throw new BadRequestException("Run is already closed");
        }
    }

    private void advanceRun(ParticipantRun run, Integer periodNumber, LocalDateTime checkpointTime) {
        if (run.getRunStatus() == ParticipantRunStatus.NEW) {
            run.setRunStatus(ParticipantRunStatus.IN_PROGRESS);
        }
        if (run.getStartedAt() == null) {
            run.setStartedAt(LocalDateTime.now());
        }
        run.setCurrentPeriodNumber(Math.max(run.getCurrentPeriodNumber(), periodNumber));
        run.setLastCheckpointAt(checkpointTime);
        participantRunRepository.save(run);

        ParticipantAccount account = run.getParticipantAccount();
        if (account.getStatus() == ParticipantAccountStatus.NEW || account.getStatus() == ParticipantAccountStatus.ASSIGNED) {
            account.setStatus(ParticipantAccountStatus.STARTED);
            participantAccountRepository.save(account);
        }
    }
}
