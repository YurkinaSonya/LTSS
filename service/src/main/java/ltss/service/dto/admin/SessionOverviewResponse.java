package ltss.service.dto.admin;

public record SessionOverviewResponse(
        Long sessionDefinitionId,
        String sessionDefinitionCode,
        Integer participantCountPlanned,
        long createdAccounts,
        long startedRuns,
        long completedRuns
) {
}
