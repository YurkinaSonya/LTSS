package ltss.service.controller;

import lombok.RequiredArgsConstructor;
import ltss.service.dto.export.AccountRunExportResponse;
import ltss.service.dto.export.CheckpointExportResponse;
import ltss.service.dto.export.SessionSummaryExportResponse;
import ltss.service.dto.export.SurveyExportResponse;
import ltss.service.service.ExportService;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

@RestController
@RequiredArgsConstructor
@RequestMapping("/api/admin/export/session-definition")
public class AdminExportController {

    private final ExportService exportService;

    @GetMapping("/{id}/summary")
    public SessionSummaryExportResponse exportSummary(@PathVariable Long id) {
        return exportService.exportSummary(id);
    }

    @GetMapping("/{id}/checkpoints")
    public CheckpointExportResponse exportCheckpoints(@PathVariable Long id) {
        return exportService.exportCheckpoints(id);
    }

    @GetMapping("/{id}/surveys")
    public SurveyExportResponse exportSurveys(@PathVariable Long id) {
        return exportService.exportSurveys(id);
    }

    @GetMapping("/{id}/accounts-runs")
    public AccountRunExportResponse exportAccountsRuns(@PathVariable Long id) {
        return exportService.exportAccountsRuns(id);
    }
}
