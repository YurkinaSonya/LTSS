package ltss.service.dto.export;

import java.util.List;

public record AccountRunExportResponse(
        Long sessionDefinitionId,
        String sessionDefinitionCode,
        List<AccountRunExportItem> items
) {
}
