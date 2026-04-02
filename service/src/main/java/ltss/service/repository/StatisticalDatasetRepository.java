package ltss.service.repository;

import java.util.List;
import java.util.Optional;
import ltss.service.entity.StatisticalDataset;
import org.springframework.data.jpa.repository.JpaRepository;

public interface StatisticalDatasetRepository extends JpaRepository<StatisticalDataset, Long> {

    Optional<StatisticalDataset> findByCode(String code);

    boolean existsByCode(String code);

    List<StatisticalDataset> findAllByOrderByCreatedAtDesc();
}