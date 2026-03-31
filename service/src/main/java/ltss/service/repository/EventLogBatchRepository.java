package ltss.service.repository;

import java.util.List;
import ltss.service.entity.EventLogBatch;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

public interface EventLogBatchRepository extends JpaRepository<EventLogBatch, Long> {

    List<EventLogBatch> findByParticipantRunIdOrderByCreatedAtAsc(String participantRunId);

    @Query("""
            select e
            from EventLogBatch e
            join fetch e.participantRun r
            join fetch r.participantAccount a
            where a.sessionDefinition.id = :sessionDefinitionId
            order by a.id asc, r.createdAt asc, e.createdAt asc
            """)
    List<EventLogBatch> findAllBySessionDefinitionId(@Param("sessionDefinitionId") Long sessionDefinitionId);
}
