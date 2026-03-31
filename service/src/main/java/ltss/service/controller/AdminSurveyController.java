package ltss.service.controller;

import jakarta.validation.Valid;
import java.util.List;
import lombok.RequiredArgsConstructor;
import ltss.service.dto.admin.SurveyTemplateRequest;
import ltss.service.dto.admin.SurveyTemplateResponse;
import ltss.service.service.SurveyAdminService;
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
@RequestMapping("/api/admin/surveys")
public class AdminSurveyController {

    private final SurveyAdminService surveyAdminService;

    @PostMapping
    @ResponseStatus(HttpStatus.CREATED)
    public SurveyTemplateResponse create(@Valid @RequestBody SurveyTemplateRequest request) {
        return surveyAdminService.create(request);
    }

    @PutMapping("/{id}")
    public SurveyTemplateResponse update(@PathVariable Long id, @Valid @RequestBody SurveyTemplateRequest request) {
        return surveyAdminService.update(id, request);
    }

    @GetMapping
    public List<SurveyTemplateResponse> list() {
        return surveyAdminService.list();
    }
}
