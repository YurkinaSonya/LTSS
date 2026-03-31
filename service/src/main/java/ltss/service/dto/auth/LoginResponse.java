package ltss.service.dto.auth;

import ltss.service.dto.run.CurrentRunResponse;

public record LoginResponse(
        String token,
        CurrentRunResponse run
) {
}
