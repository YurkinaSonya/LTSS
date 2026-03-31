package ltss.service.repository;

import java.util.List;
import ltss.service.entity.SurveySubmission;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

public interface SurveySubmissionRepository extends JpaRepository<SurveySubmission, Long> {

    @Query("""
            select s
            from SurveySubmission s
            join fetch s.participantRun r
            join fetch r.participantAccount a
            join fetch s.surveyTemplate t
            where a.sessionDefinition.id = :sessionDefinitionId
            order by a.id asc, r.createdAt asc, s.createdAt asc
            """)
    List<SurveySubmission> findAllBySessionDefinitionId(@Param("sessionDefinitionId") Long sessionDefinitionId);
}
