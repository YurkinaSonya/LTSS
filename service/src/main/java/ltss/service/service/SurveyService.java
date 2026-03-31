package ltss.service.service;

import java.time.LocalDateTime;
import lombok.RequiredArgsConstructor;
import ltss.service.dto.run.SurveySubmissionRequest;
import ltss.service.dto.run.SurveySubmissionResponse;
import ltss.service.entity.ParticipantRun;
import ltss.service.entity.SurveySubmission;
import ltss.service.entity.SurveyTemplate;
import ltss.service.exception.BadRequestException;
import ltss.service.exception.NotFoundException;
import ltss.service.repository.SurveySubmissionRepository;
import ltss.service.repository.SurveyTemplateRepository;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

@Service
@RequiredArgsConstructor
public class SurveyService {

    private final SurveyTemplateRepository surveyTemplateRepository;
    private final SurveySubmissionRepository surveySubmissionRepository;

    @Transactional
    public SurveySubmissionResponse submitSurvey(ParticipantRun run, SurveySubmissionRequest request) {
        SurveyTemplate surveyTemplate = surveyTemplateRepository.findById(request.surveyTemplateId())
                .orElseThrow(() -> new NotFoundException("Survey template not found: " + request.surveyTemplateId()));

        Long sessionDefinitionId = run.getParticipantAccount().getSessionDefinition().getId();
        if (!surveyTemplate.getSessionDefinition().getId().equals(sessionDefinitionId)) {
            throw new BadRequestException("Survey template does not belong to the participant session");
        }

        SurveySubmission submission = new SurveySubmission();
        submission.setParticipantRun(run);
        submission.setSurveyTemplate(surveyTemplate);
        submission.setPeriodNumber(request.periodNumber());
        submission.setResponseJson(request.responseJson());
        submission.setSubmittedAt(request.submittedAt() != null ? request.submittedAt() : LocalDateTime.now());

        SurveySubmission savedSubmission = surveySubmissionRepository.save(submission);
        return new SurveySubmissionResponse(
                savedSubmission.getId(),
                run.getId(),
                surveyTemplate.getId(),
                savedSubmission.getPeriodNumber(),
                savedSubmission.getSubmittedAt()
        );
    }
}
