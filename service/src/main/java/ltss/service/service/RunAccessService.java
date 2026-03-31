package ltss.service.service;

import jakarta.servlet.http.HttpServletRequest;
import lombok.RequiredArgsConstructor;
import ltss.service.entity.ParticipantRun;
import ltss.service.exception.UnauthorizedException;
import ltss.service.repository.ParticipantRunRepository;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

@Service
@RequiredArgsConstructor
public class RunAccessService {

    private final ParticipantRunRepository participantRunRepository;
    private final RunTokenService runTokenService;

    @Transactional(readOnly = true)
    public ParticipantRun getAuthorizedRun(HttpServletRequest request) {
        RunTokenService.RunTokenPayload payload = runTokenService.parseToken(extractToken(request));
        ParticipantRun run = participantRunRepository.findDetailedById(payload.runId())
                .orElseThrow(() -> new UnauthorizedException("Run token is no longer valid"));

        if (!run.getParticipantAccount().getId().equals(payload.participantAccountId())) {
            throw new UnauthorizedException("Run token is no longer valid");
        }
        return run;
    }

    @Transactional(readOnly = true)
    public ParticipantRun getAuthorizedRun(HttpServletRequest request, String expectedRunId) {
        ParticipantRun run = getAuthorizedRun(request);
        if (!run.getId().equals(expectedRunId)) {
            throw new UnauthorizedException("Token does not match requested run");
        }
        return run;
    }

    private String extractToken(HttpServletRequest request) {
        String authorization = request.getHeader("Authorization");
        if (authorization != null && authorization.startsWith("Bearer ")) {
            return authorization.substring(7);
        }
        return request.getHeader("X-Run-Token");
    }
}
