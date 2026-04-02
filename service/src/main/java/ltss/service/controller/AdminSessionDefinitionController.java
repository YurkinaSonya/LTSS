package ltss.service.controller;

import jakarta.validation.Valid;
import java.util.List;
import lombok.RequiredArgsConstructor;
import ltss.service.dto.admin.GenerateAccountsRequest;
import ltss.service.dto.admin.GeneratedAccountCredentialResponse;
import ltss.service.dto.admin.ParticipantAccountResponse;
import ltss.service.dto.admin.ParticipantRunResponse;
import ltss.service.dto.admin.SessionDefinitionRequest;
import ltss.service.dto.admin.SessionDefinitionResponse;
import ltss.service.dto.admin.SessionOverviewResponse;
import ltss.service.service.SessionDefinitionAdminService;
import org.springframework.http.HttpStatus;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.PutMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.ResponseStatus;
import org.springframework.web.bind.annotation.RestController;

@RestController
@RequiredArgsConstructor
@RequestMapping("/api/admin/session-definitions")
public class AdminSessionDefinitionController {

    private final SessionDefinitionAdminService sessionDefinitionAdminService;

    @PostMapping
    @ResponseStatus(HttpStatus.CREATED)
    public SessionDefinitionResponse create(@Valid @RequestBody SessionDefinitionRequest request) {
        return sessionDefinitionAdminService.create(request);
    }

    @PutMapping("/{id}")
    public SessionDefinitionResponse update(@PathVariable Long id, @Valid @RequestBody SessionDefinitionRequest request) {
        return sessionDefinitionAdminService.update(id, request);
    }

    @GetMapping("/{id}")
    public SessionDefinitionResponse get(@PathVariable Long id) {
        return sessionDefinitionAdminService.get(id);
    }

    @GetMapping
    public List<SessionDefinitionResponse> list() {
        return sessionDefinitionAdminService.list();
    }

    @PostMapping("/{id}/generate-accounts")
    public List<GeneratedAccountCredentialResponse> generateAccounts(
            @PathVariable Long id,
            @Valid @RequestBody GenerateAccountsRequest request) {
        return sessionDefinitionAdminService.generateAccounts(id, request);
    }

    @GetMapping("/{id}/accounts")
    public List<ParticipantAccountResponse> listAccounts(@PathVariable Long id) {
        return sessionDefinitionAdminService.listAccounts(id);
    }

    @GetMapping("/{id}/runs")
    public List<ParticipantRunResponse> listRuns(@PathVariable Long id) {
        return sessionDefinitionAdminService.listRuns(id);
    }

    @GetMapping("/{id}/overview")
    public SessionOverviewResponse getOverview(@PathVariable Long id) {
        return sessionDefinitionAdminService.getOverview(id);
    }
}
