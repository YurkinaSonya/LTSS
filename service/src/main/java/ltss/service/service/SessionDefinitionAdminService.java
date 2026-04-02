package ltss.service.service;

import java.security.SecureRandom;
import java.util.ArrayList;
import java.util.List;
import lombok.RequiredArgsConstructor;
import ltss.service.dto.admin.GenerateAccountsRequest;
import ltss.service.dto.admin.GeneratedAccountCredentialResponse;
import ltss.service.dto.admin.ParticipantAccountResponse;
import ltss.service.dto.admin.ParticipantRunResponse;
import ltss.service.dto.admin.SessionDefinitionRequest;
import ltss.service.dto.admin.SessionDefinitionResponse;
import ltss.service.dto.admin.SessionOverviewResponse;
import ltss.service.entity.ExperimentSessionDefinition;
import ltss.service.entity.ParticipantAccount;
import ltss.service.entity.ParticipantRun;
import ltss.service.enums.GroupAssignmentStrategy;
import ltss.service.enums.ParticipantAccountStatus;
import ltss.service.enums.ParticipantRunStatus;
import ltss.service.exception.BadRequestException;
import ltss.service.exception.ConflictException;
import ltss.service.exception.NotFoundException;
import ltss.service.mapper.ParticipantMapper;
import ltss.service.mapper.SessionDefinitionMapper;
import ltss.service.repository.ExperimentSessionDefinitionRepository;
import ltss.service.repository.ParticipantAccountRepository;
import ltss.service.repository.ParticipantRunRepository;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;
import org.springframework.util.StringUtils;

@Service
@RequiredArgsConstructor
public class SessionDefinitionAdminService {

    private static final char[] PASSWORD_ALPHABET = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789".toCharArray();

    private final ExperimentSessionDefinitionRepository sessionDefinitionRepository;
    private final ParticipantAccountRepository participantAccountRepository;
    private final ParticipantRunRepository participantRunRepository;
    private final SessionDefinitionMapper sessionDefinitionMapper;
    private final ParticipantMapper participantMapper;
    private final PasswordService passwordService;

    private final SecureRandom secureRandom = new SecureRandom();

    @Transactional
    public SessionDefinitionResponse create(SessionDefinitionRequest request) {
        if (sessionDefinitionRepository.existsByCode(request.code())) {
            throw new ConflictException("Session definition code already exists: " + request.code());
        }

        ExperimentSessionDefinition entity = new ExperimentSessionDefinition();
        sessionDefinitionMapper.updateEntity(entity, request);
        return sessionDefinitionMapper.toResponse(sessionDefinitionRepository.save(entity));
    }

    @Transactional
    public SessionDefinitionResponse update(Long id, SessionDefinitionRequest request) {
        ExperimentSessionDefinition entity = getSessionDefinition(id);
        if (!entity.getCode().equals(request.code()) && sessionDefinitionRepository.existsByCode(request.code())) {
            throw new ConflictException("Session definition code already exists: " + request.code());
        }

        sessionDefinitionMapper.updateEntity(entity, request);
        return sessionDefinitionMapper.toResponse(sessionDefinitionRepository.save(entity));
    }

    @Transactional(readOnly = true)
    public SessionDefinitionResponse get(Long id) {
        return sessionDefinitionMapper.toResponse(getSessionDefinition(id));
    }

    @Transactional(readOnly = true)
    public List<SessionDefinitionResponse> list() {
        return sessionDefinitionRepository.findAllByOrderByCreatedAtDesc()
                .stream()
                .map(sessionDefinitionMapper::toResponse)
                .toList();
    }

    @Transactional
    public List<GeneratedAccountCredentialResponse> generateAccounts(Long sessionDefinitionId, GenerateAccountsRequest request) {
        ExperimentSessionDefinition sessionDefinition = getSessionDefinition(sessionDefinitionId);
        GroupAssignmentStrategy strategy = request.assignedGroupStrategy() != null
                ? request.assignedGroupStrategy()
                : GroupAssignmentStrategy.NONE;
        List<String> groupCodes = request.groupCodes() == null ? List.of() : request.groupCodes().stream()
                .filter(StringUtils::hasText)
                .toList();

        if (strategy == GroupAssignmentStrategy.ROUND_ROBIN && groupCodes.isEmpty()) {
            throw new BadRequestException("groupCodes are required for ROUND_ROBIN strategy");
        }

        String prefix = sanitizePrefix(request.prefix(), sessionDefinition.getCode());
        long existingCount = participantAccountRepository.countBySessionDefinitionId(sessionDefinitionId);
        List<GeneratedAccountCredentialResponse> createdAccounts = new ArrayList<>();

        for (int index = 0; index < request.count(); index++) {
            String login = buildUniqueLogin(prefix, sessionDefinition.getId(), existingCount + index + 1L);
            String plainPassword = generatePassword();
            String assignedGroupCode = resolveGroupCode(strategy, groupCodes, index);

            ParticipantAccount account = new ParticipantAccount();
            account.setSessionDefinition(sessionDefinition);
            account.setLogin(login);
            account.setPasswordHash(passwordService.hashPassword(plainPassword));
            account.setStatus(StringUtils.hasText(assignedGroupCode)
                    ? ParticipantAccountStatus.ASSIGNED
                    : ParticipantAccountStatus.NEW);
            account.setAssignedGroupCode(assignedGroupCode);
            account.setAssignedConfigJson(resolveAssignedConfigJson(assignedGroupCode, request.defaultAssignedConfigJson()));

            ParticipantAccount savedAccount = participantAccountRepository.save(account);
            createdAccounts.add(new GeneratedAccountCredentialResponse(
                    savedAccount.getId(),
                    savedAccount.getLogin(),
                    plainPassword,
                    savedAccount.getAssignedGroupCode()
            ));
        }

        return createdAccounts;
    }

    @Transactional(readOnly = true)
    public List<ParticipantAccountResponse> listAccounts(Long sessionDefinitionId) {
        getSessionDefinition(sessionDefinitionId);
        return participantAccountRepository.findBySessionDefinitionIdOrderByIdAsc(sessionDefinitionId)
                .stream()
                .map(participantMapper::toParticipantAccountResponse)
                .toList();
    }

    @Transactional(readOnly = true)
    public List<ParticipantRunResponse> listRuns(Long sessionDefinitionId) {
        getSessionDefinition(sessionDefinitionId);
        return participantRunRepository.findByParticipantAccount_SessionDefinition_IdOrderByCreatedAtDesc(sessionDefinitionId)
                .stream()
                .map(participantMapper::toParticipantRunResponse)
                .toList();
    }

    @Transactional(readOnly = true)
    public SessionOverviewResponse getOverview(Long sessionDefinitionId) {
        ExperimentSessionDefinition sessionDefinition = getSessionDefinition(sessionDefinitionId);
        long createdAccounts = participantAccountRepository.countBySessionDefinitionId(sessionDefinitionId);
        List<ParticipantRun> runs = participantRunRepository
                .findByParticipantAccount_SessionDefinition_IdOrderByCreatedAtDesc(sessionDefinitionId);

        long startedRuns = runs.stream()
                .filter(run -> run.getStartedAt() != null || run.getRunStatus() != ParticipantRunStatus.NEW)
                .count();
        long completedRuns = runs.stream()
                .filter(run -> run.getRunStatus() == ParticipantRunStatus.COMPLETED)
                .count();

        return new SessionOverviewResponse(
                sessionDefinition.getId(),
                sessionDefinition.getCode(),
                sessionDefinition.getParticipantCountPlanned(),
                createdAccounts,
                startedRuns,
                completedRuns
        );
    }

    private ExperimentSessionDefinition getSessionDefinition(Long id) {
        return sessionDefinitionRepository.findById(id)
                .orElseThrow(() -> new NotFoundException("Session definition not found: " + id));
    }

    private String sanitizePrefix(String requestedPrefix, String fallbackPrefix) {
        String candidate = StringUtils.hasText(requestedPrefix) ? requestedPrefix : fallbackPrefix;
        String sanitized = candidate.replaceAll("[^A-Za-z0-9]+", "").toLowerCase();
        return StringUtils.hasText(sanitized) ? sanitized : "participant";
    }

    private String buildUniqueLogin(String prefix, Long sessionDefinitionId, long sequence) {
        long currentSequence = sequence;
        String login = prefix + "-" + sessionDefinitionId + "-" + String.format("%04d", currentSequence);
        while (participantAccountRepository.existsByLogin(login)) {
            currentSequence++;
            login = prefix + "-" + sessionDefinitionId + "-" + String.format("%04d", currentSequence);
        }
        return login;
    }

    private String resolveGroupCode(GroupAssignmentStrategy strategy, List<String> groupCodes, int index) {
        if (strategy != GroupAssignmentStrategy.ROUND_ROBIN || groupCodes.isEmpty()) {
            return null;
        }
        return groupCodes.get(index % groupCodes.size());
    }

    private String resolveAssignedConfigJson(String assignedGroupCode, String defaultAssignedConfigJson) {
        if (StringUtils.hasText(defaultAssignedConfigJson)) {
            return defaultAssignedConfigJson;
        }
        if (!StringUtils.hasText(assignedGroupCode)) {
            return null;
        }
        return "{\"groupCode\":\"" + assignedGroupCode.replace("\"", "\\\"") + "\"}";
    }

    private String generatePassword() {
        int passwordLength = 12;
        StringBuilder builder = new StringBuilder(passwordLength);
        for (int index = 0; index < passwordLength; index++) {
            builder.append(PASSWORD_ALPHABET[secureRandom.nextInt(PASSWORD_ALPHABET.length)]);
        }
        return builder.toString();
    }
}
