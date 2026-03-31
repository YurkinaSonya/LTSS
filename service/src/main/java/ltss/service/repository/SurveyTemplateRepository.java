package ltss.service.repository;

import java.util.List;
import ltss.service.entity.SurveyTemplate;
import org.springframework.data.jpa.repository.JpaRepository;

public interface SurveyTemplateRepository extends JpaRepository<SurveyTemplate, Long> {

    List<SurveyTemplate> findBySessionDefinitionIdOrderByTypeAscIdAsc(Long sessionDefinitionId);
}
