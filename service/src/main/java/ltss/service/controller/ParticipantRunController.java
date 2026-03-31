package ltss.service.controller;

import jakarta.servlet.http.HttpServletRequest;
import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;
import ltss.service.dto.run.BootstrapResponse;
import ltss.service.dto.run.CheckpointRequest;
import ltss.service.dto.run.CheckpointResponse;
import ltss.service.dto.run.CompleteRunResponse;
import ltss.service.dto.run.CurrentRunResponse;
import ltss.service.dto.run.EventLogBatchRequest;
import ltss.service.dto.run.EventLogBatchResponse;
import ltss.service.dto.run.SurveySubmissionRequest;
import ltss.service.dto.run.SurveySubmissionResponse;
import ltss.service.entity.ParticipantRun;
import ltss.service.service.BootstrapService;
import ltss.service.service.LoggingService;
import ltss.service.service.RunAccessService;
import ltss.service.service.RunService;
import ltss.service.service.SurveyService;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

@RestController
@RequiredArgsConstructor
@RequestMapping("/api/run")
public class ParticipantRunController {

    private final RunAccessService runAccessService;
    private final RunService runService;
    private final BootstrapService bootstrapService;
    private final SurveyService surveyService;
    private final LoggingService loggingService;

    @GetMapping("/current")
    public CurrentRunResponse getCurrent(HttpServletRequest request) {
        ParticipantRun run = runAccessService.getAuthorizedRun(request);
        return runService.getCurrentRun(run);
    }

    @GetMapping("/{runId}/bootstrap")
    public BootstrapResponse getBootstrap(@PathVariable String runId, HttpServletRequest request) {
        ParticipantRun run = runAccessService.getAuthorizedRun(request, runId);
        return bootstrapService.buildBootstrap(run);
    }

    @PostMapping("/{runId}/checkpoint")
    public CheckpointResponse submitCheckpoint(
            @PathVariable String runId,
            @Valid @RequestBody CheckpointRequest checkpointRequest,
            HttpServletRequest request) {
        ParticipantRun run = runAccessService.getAuthorizedRun(request, runId);
        return runService.submitCheckpoint(run, checkpointRequest);
    }

    @PostMapping("/{runId}/surveys/submit")
    public SurveySubmissionResponse submitSurvey(
            @PathVariable String runId,
            @Valid @RequestBody SurveySubmissionRequest surveySubmissionRequest,
            HttpServletRequest request) {
        ParticipantRun run = runAccessService.getAuthorizedRun(request, runId);
        return surveyService.submitSurvey(run, surveySubmissionRequest);
    }

    @PostMapping("/{runId}/logs/batch")
    public EventLogBatchResponse submitLogBatch(
            @PathVariable String runId,
            @Valid @RequestBody EventLogBatchRequest eventLogBatchRequest,
            HttpServletRequest request) {
        ParticipantRun run = runAccessService.getAuthorizedRun(request, runId);
        return loggingService.storeBatch(run, eventLogBatchRequest);
    }

    @PostMapping("/{runId}/complete")
    public CompleteRunResponse completeRun(@PathVariable String runId, HttpServletRequest request) {
        ParticipantRun run = runAccessService.getAuthorizedRun(request, runId);
        return runService.completeRun(run);
    }
}
