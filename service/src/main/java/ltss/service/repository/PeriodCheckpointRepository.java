package ltss.service.repository;

import java.util.List;
import java.util.Optional;
import ltss.service.entity.PeriodCheckpoint;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

public interface PeriodCheckpointRepository extends JpaRepository<PeriodCheckpoint, Long> {

    List<PeriodCheckpoint> findByParticipantRunIdOrderByPeriodNumberAscCreatedAtAsc(String participantRunId);

    Optional<PeriodCheckpoint> findTopByParticipantRunIdAndPeriodNumberOrderByCreatedAtDesc(
            String participantRunId, Integer periodNumber);

    @Query("""
            select p
            from PeriodCheckpoint p
            join fetch p.participantRun r
            join fetch r.participantAccount a
            where a.sessionDefinition.id = :sessionDefinitionId
            order by a.id asc, r.createdAt asc, p.periodNumber asc, p.createdAt asc
            """)
    List<PeriodCheckpoint> findAllBySessionDefinitionId(@Param("sessionDefinitionId") Long sessionDefinitionId);
}
