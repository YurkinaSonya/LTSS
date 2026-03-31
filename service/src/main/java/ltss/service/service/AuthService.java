package ltss.service.service;

import java.time.LocalDateTime;
import java.util.UUID;
import lombok.RequiredArgsConstructor;
import ltss.service.dto.auth.LoginRequest;
import ltss.service.dto.auth.LoginResponse;
import ltss.service.entity.ParticipantAccount;
import ltss.service.entity.ParticipantRun;
import ltss.service.enums.ParticipantAccountStatus;
import ltss.service.enums.ParticipantRunStatus;
import ltss.service.exception.UnauthorizedException;
import ltss.service.mapper.ParticipantMapper;
import ltss.service.repository.ParticipantAccountRepository;
import ltss.service.repository.ParticipantRunRepository;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

@Service
@RequiredArgsConstructor
public class AuthService {

    private final ParticipantAccountRepository participantAccountRepository;
    private final ParticipantRunRepository participantRunRepository;
    private final PasswordService passwordService;
    private final RunTokenService runTokenService;
    private final ParticipantMapper participantMapper;

    @Transactional
    public LoginResponse login(LoginRequest request) {
        ParticipantAccount account = participantAccountRepository.findByLogin(request.login())
                .orElseThrow(() -> new UnauthorizedException("Invalid login or password"));

        if (account.getStatus() == ParticipantAccountStatus.BLOCKED
                || !passwordService.matches(request.password(), account.getPasswordHash())) {
            throw new UnauthorizedException("Invalid login or password");
        }

        ParticipantRun run = participantRunRepository.findTopByParticipantAccountIdOrderByCreatedAtDesc(account.getId())
                .orElseGet(() -> createRun(account));

        if (run.getRunStatus() == ParticipantRunStatus.NEW) {
            run.setRunStatus(ParticipantRunStatus.IN_PROGRESS);
        }
        if (run.getStartedAt() == null) {
            run.setStartedAt(LocalDateTime.now());
        }
        if (account.getStatus() == ParticipantAccountStatus.NEW || account.getStatus() == ParticipantAccountStatus.ASSIGNED) {
            account.setStatus(ParticipantAccountStatus.STARTED);
        }

        participantAccountRepository.save(account);
        participantRunRepository.save(run);

        return new LoginResponse(
                runTokenService.createToken(run.getId(), account.getId()),
                participantMapper.toCurrentRunResponse(run)
        );
    }

    private ParticipantRun createRun(ParticipantAccount account) {
        ParticipantRun run = new ParticipantRun();
        run.setId(UUID.randomUUID().toString());
        run.setParticipantAccount(account);
        run.setRunStatus(ParticipantRunStatus.IN_PROGRESS);
        run.setCurrentPeriodNumber(0);
        run.setBootstrapVersion(account.getSessionDefinition().getConfigVersion());
        run.setStartedAt(LocalDateTime.now());
        return run;
    }
}
