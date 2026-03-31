package ltss.service.service;

import lombok.RequiredArgsConstructor;
import ltss.service.dto.run.EventLogBatchRequest;
import ltss.service.dto.run.EventLogBatchResponse;
import ltss.service.entity.EventLogBatch;
import ltss.service.entity.ParticipantRun;
import ltss.service.repository.EventLogBatchRepository;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

@Service
@RequiredArgsConstructor
public class LoggingService {

    private final EventLogBatchRepository eventLogBatchRepository;

    @Transactional
    public EventLogBatchResponse storeBatch(ParticipantRun run, EventLogBatchRequest request) {
        EventLogBatch batch = new EventLogBatch();
        batch.setParticipantRun(run);
        batch.setPeriodNumber(request.periodNumber());
        batch.setBatchType(request.batchType());
        batch.setPayloadJson(request.payloadJson());

        EventLogBatch savedBatch = eventLogBatchRepository.save(batch);
        return new EventLogBatchResponse(savedBatch.getId(), run.getId(), savedBatch.getBatchType(), savedBatch.getCreatedAt());
    }
}
