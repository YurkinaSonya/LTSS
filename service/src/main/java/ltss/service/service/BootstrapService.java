package ltss.service.service;

import java.util.List;
import lombok.RequiredArgsConstructor;
import ltss.service.dto.run.BootstrapResponse;
import ltss.service.dto.run.BootstrapStatisticalDatasetResponse;
import ltss.service.dto.run.BootstrapSurveyTemplateResponse;
import ltss.service.entity.ParticipantRun;
import ltss.service.entity.StatisticalDataset;
import ltss.service.entity.SurveyTemplate;
import ltss.service.mapper.ParticipantMapper;
import ltss.service.mapper.SessionDefinitionMapper;
import ltss.service.mapper.StatisticalDatasetMapper;
import ltss.service.mapper.SurveyTemplateMapper;
import ltss.service.repository.SurveyTemplateRepository;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

@Service
@RequiredArgsConstructor
public class BootstrapService {

    private final SurveyTemplateRepository surveyTemplateRepository;
    private final ParticipantMapper participantMapper;
    private final SessionDefinitionMapper sessionDefinitionMapper;
    private final SurveyTemplateMapper surveyTemplateMapper;
    private final StatisticalDatasetMapper statisticalDatasetMapper;

    @Transactional(readOnly = true)
    public BootstrapResponse buildBootstrap(ParticipantRun run) {
        List<BootstrapSurveyTemplateResponse> surveyTemplates = surveyTemplateRepository
                .findBySessionDefinitionIdOrderByTypeAscIdAsc(run.getParticipantAccount().getSessionDefinition().getId())
                .stream()
                .filter(SurveyTemplate::isEnabled)
                .map(surveyTemplateMapper::toBootstrapResponse)
                .toList();

        StatisticalDataset statisticalDataset = run.getParticipantAccount().getSessionDefinition().getStatisticalDataset();
        BootstrapStatisticalDatasetResponse statisticalDatasetResponse = statisticalDataset != null
                ? statisticalDatasetMapper.toBootstrapResponse(statisticalDataset)
                : null;

        return new BootstrapResponse(
                participantMapper.toBootstrapRunResponse(run),
                sessionDefinitionMapper.toBootstrapResponse(run.getParticipantAccount().getSessionDefinition()),
                participantMapper.toBootstrapParticipantResponse(run.getParticipantAccount()),
                statisticalDatasetResponse,
                surveyTemplates
        );
    }
}