package ltss.service.dto.admin;

import java.time.LocalDateTime;
import ltss.service.enums.ParticipantAccountStatus;

public record ParticipantAccountResponse(
        Long id,
        Long sessionDefinitionId,
        String login,
        ParticipantAccountStatus status,
        String assignedGroupCode,
        String assignedConfigJson,
        String deviceBindingJson,
        LocalDateTime createdAt,
        LocalDateTime updatedAt
) {
}
