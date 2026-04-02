package ltss.service.mapper;

import ltss.service.dto.admin.StatisticalDatasetRequest;
import ltss.service.dto.admin.StatisticalDatasetResponse;
import ltss.service.dto.run.BootstrapStatisticalDatasetResponse;
import ltss.service.entity.StatisticalDataset;
import org.springframework.stereotype.Component;

@Component
public class StatisticalDatasetMapper {

    public StatisticalDatasetResponse toResponse(StatisticalDataset entity) {
        return new StatisticalDatasetResponse(
                entity.getId(),
                entity.getCode(),
                entity.getTitle(),
                entity.getDescription(),
                entity.getStatus(),
                entity.getVersion(),
                entity.getDatasetJson(),
                entity.getCreatedAt(),
                entity.getUpdatedAt()
        );
    }

    public BootstrapStatisticalDatasetResponse toBootstrapResponse(StatisticalDataset entity) {
        return new BootstrapStatisticalDatasetResponse(
                entity.getId(),
                entity.getCode(),
                entity.getTitle(),
                entity.getVersion(),
                entity.getDatasetJson()
        );
    }

    public void updateEntity(StatisticalDataset entity, StatisticalDatasetRequest request) {
        entity.setCode(request.code());
        entity.setTitle(request.title());
        entity.setDescription(request.description());
        entity.setStatus(request.status());
        entity.setVersion(request.version());
        entity.setDatasetJson(request.datasetJson());
    }
}