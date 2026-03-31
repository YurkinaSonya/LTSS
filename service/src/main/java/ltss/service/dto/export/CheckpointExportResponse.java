package ltss.service.dto.export;

import java.util.List;

public record CheckpointExportResponse(
        Long sessionDefinitionId,
        String sessionDefinitionCode,
        List<CheckpointExportItem> items
) {
}
