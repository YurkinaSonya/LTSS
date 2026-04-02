package ltss.service.service;

import java.util.List;
import lombok.RequiredArgsConstructor;
import ltss.service.dto.admin.StatisticalDatasetRequest;
import ltss.service.dto.admin.StatisticalDatasetResponse;
import ltss.service.entity.StatisticalDataset;
import ltss.service.exception.BadRequestException;
import ltss.service.exception.ConflictException;
import ltss.service.exception.NotFoundException;
import ltss.service.mapper.StatisticalDatasetMapper;
import ltss.service.repository.StatisticalDatasetRepository;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

@Service
@RequiredArgsConstructor
public class StatisticalDatasetAdminService {

    private final StatisticalDatasetRepository statisticalDatasetRepository;
    private final StatisticalDatasetMapper statisticalDatasetMapper;

    @Transactional
    public StatisticalDatasetResponse create(StatisticalDatasetRequest request) {
        validateDatasetJson(request.datasetJson());
        if (statisticalDatasetRepository.existsByCode(request.code())) {
            throw new ConflictException("Statistical dataset code already exists: " + request.code());
        }

        StatisticalDataset entity = new StatisticalDataset();
        statisticalDatasetMapper.updateEntity(entity, request);
        return statisticalDatasetMapper.toResponse(statisticalDatasetRepository.save(entity));
    }

    @Transactional
    public StatisticalDatasetResponse update(Long id, StatisticalDatasetRequest request) {
        validateDatasetJson(request.datasetJson());
        StatisticalDataset entity = getDatasetEntity(id);
        if (!entity.getCode().equals(request.code()) && statisticalDatasetRepository.existsByCode(request.code())) {
            throw new ConflictException("Statistical dataset code already exists: " + request.code());
        }

        statisticalDatasetMapper.updateEntity(entity, request);
        return statisticalDatasetMapper.toResponse(statisticalDatasetRepository.save(entity));
    }

    @Transactional(readOnly = true)
    public StatisticalDatasetResponse get(Long id) {
        return statisticalDatasetMapper.toResponse(getDatasetEntity(id));
    }

    @Transactional(readOnly = true)
    public List<StatisticalDatasetResponse> list() {
        return statisticalDatasetRepository.findAllByOrderByCreatedAtDesc()
                .stream()
                .map(statisticalDatasetMapper::toResponse)
                .toList();
    }

    @Transactional(readOnly = true)
    public StatisticalDataset getDatasetEntity(Long id) {
        return statisticalDatasetRepository.findById(id)
                .orElseThrow(() -> new NotFoundException("Statistical dataset not found: " + id));
    }

    private void validateDatasetJson(String datasetJson) {
        String trimmed = datasetJson == null ? "" : datasetJson.trim();
        if (trimmed.isEmpty()) {
            throw new BadRequestException("datasetJson is required");
        }
        if (!trimmed.startsWith("[") || !trimmed.endsWith("]")) {
            throw new BadRequestException("datasetJson must be a JSON array string");
        }
    }
}