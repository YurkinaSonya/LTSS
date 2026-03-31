package ltss.service.dto.admin;

public record GeneratedAccountCredentialResponse(
        Long participantAccountId,
        String login,
        String plainPassword,
        String assignedGroupCode
) {
}
