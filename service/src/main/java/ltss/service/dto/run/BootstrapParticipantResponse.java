package ltss.service.dto.run;

public record BootstrapParticipantResponse(
        Long participantAccountId,
        String login,
        String assignedGroupCode,
        String assignedConfigJson,
        String deviceBindingJson
) {
}
