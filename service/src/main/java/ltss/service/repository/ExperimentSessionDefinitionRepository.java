package ltss.service.repository;

import java.util.List;
import java.util.Optional;
import ltss.service.entity.ExperimentSessionDefinition;
import org.springframework.data.jpa.repository.JpaRepository;

public interface ExperimentSessionDefinitionRepository extends JpaRepository<ExperimentSessionDefinition, Long> {

    Optional<ExperimentSessionDefinition> findByCode(String code);

    boolean existsByCode(String code);

    List<ExperimentSessionDefinition> findAllByOrderByCreatedAtDesc();
}
