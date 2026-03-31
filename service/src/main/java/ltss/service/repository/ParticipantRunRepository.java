package ltss.service.repository;

import java.util.List;
import java.util.Optional;
import ltss.service.entity.ParticipantRun;
import ltss.service.enums.ParticipantRunStatus;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

public interface ParticipantRunRepository extends JpaRepository<ParticipantRun, String> {

    Optional<ParticipantRun> findTopByParticipantAccountIdOrderByCreatedAtDesc(Long participantAccountId);

    List<ParticipantRun> findByParticipantAccount_SessionDefinition_IdOrderByCreatedAtDesc(Long sessionDefinitionId);

    List<ParticipantRun> findByParticipantAccount_SessionDefinition_IdAndRunStatusOrderByCreatedAtDesc(
            Long sessionDefinitionId, ParticipantRunStatus runStatus);

    @Query("""
            select r
            from ParticipantRun r
            join fetch r.participantAccount a
            join fetch a.sessionDefinition s
            where r.id = :id
            """)
    Optional<ParticipantRun> findDetailedById(@Param("id") String id);
}
