package ltss.service.controller;

import jakarta.validation.Valid;
import java.util.List;
import lombok.RequiredArgsConstructor;
import ltss.service.dto.admin.StatisticalDatasetRequest;
import ltss.service.dto.admin.StatisticalDatasetResponse;
import ltss.service.service.StatisticalDatasetAdminService;
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
@RequestMapping("/api/admin/statistical-datasets")
public class AdminStatisticalDatasetController {

    private final StatisticalDatasetAdminService statisticalDatasetAdminService;

    @PostMapping
    @ResponseStatus(HttpStatus.CREATED)
    public StatisticalDatasetResponse create(@Valid @RequestBody StatisticalDatasetRequest request) {
        return statisticalDatasetAdminService.create(request);
    }

    @PutMapping("/{id}")
    public StatisticalDatasetResponse update(@PathVariable Long id, @Valid @RequestBody StatisticalDatasetRequest request) {
        return statisticalDatasetAdminService.update(id, request);
    }

    @GetMapping
    public List<StatisticalDatasetResponse> list() {
        return statisticalDatasetAdminService.list();
    }

    @GetMapping("/{id}")
    public StatisticalDatasetResponse get(@PathVariable Long id) {
        return statisticalDatasetAdminService.get(id);
    }
}