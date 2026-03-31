package ltss.service.service;

import java.util.List;
import lombok.RequiredArgsConstructor;
import ltss.service.dto.admin.SurveyTemplateRequest;
import ltss.service.dto.admin.SurveyTemplateResponse;
import ltss.service.entity.SurveyTemplate;
import ltss.service.exception.NotFoundException;
import ltss.service.mapper.SurveyTemplateMapper;
import ltss.service.repository.ExperimentSessionDefinitionRepository;
import ltss.service.repository.SurveyTemplateRepository;
import org.springframework.data.domain.Sort;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

@Service
@RequiredArgsConstructor
public class SurveyAdminService {

    private final SurveyTemplateRepository surveyTemplateRepository;
    private final ExperimentSessionDefinitionRepository sessionDefinitionRepository;
    private final SurveyTemplateMapper surveyTemplateMapper;

    @Transactional
    public SurveyTemplateResponse create(SurveyTemplateRequest request) {
        SurveyTemplate entity = new SurveyTemplate();
        entity.setSessionDefinition(sessionDefinitionRepository.findById(request.sessionDefinitionId())
                .orElseThrow(() -> new NotFoundException("Session definition not found: " + request.sessionDefinitionId())));
        surveyTemplateMapper.updateEntity(entity, request);
        return surveyTemplateMapper.toResponse(surveyTemplateRepository.save(entity));
    }

    @Transactional
    public SurveyTemplateResponse update(Long id, SurveyTemplateRequest request) {
        SurveyTemplate entity = surveyTemplateRepository.findById(id)
                .orElseThrow(() -> new NotFoundException("Survey template not found: " + id));
        entity.setSessionDefinition(sessionDefinitionRepository.findById(request.sessionDefinitionId())
                .orElseThrow(() -> new NotFoundException("Session definition not found: " + request.sessionDefinitionId())));
        surveyTemplateMapper.updateEntity(entity, request);
        return surveyTemplateMapper.toResponse(surveyTemplateRepository.save(entity));
    }

    @Transactional(readOnly = true)
    public List<SurveyTemplateResponse> list() {
        return surveyTemplateRepository.findAll(Sort.by(Sort.Direction.DESC, "createdAt"))
                .stream()
                .map(surveyTemplateMapper::toResponse)
                .toList();
    }
}
