package ltss.service.dto.run;

public record BootstrapStatisticalDatasetResponse(
        Long id,
        String code,
        String title,
        Integer version,
        String datasetJson
) {
}