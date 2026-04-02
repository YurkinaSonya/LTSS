package ltss.service.dto.run;

import ltss.service.enums.ExperimentSessionStatus;

public record BootstrapSessionResponse(
        Long sessionDefinitionId,
        String code,
        String title,
        String description,
        ExperimentSessionStatus status,
        Integer configVersion,
        Integer participantCountPlanned,
        Long statisticalDatasetId,
        String sessionConfigJson
) {
}