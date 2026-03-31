package ltss.service.dto.run;

import java.time.LocalDateTime;
import ltss.service.enums.ParticipantRunStatus;

public record CompleteRunResponse(
        String runId,
        ParticipantRunStatus runStatus,
        LocalDateTime finishedAt
) {
}
