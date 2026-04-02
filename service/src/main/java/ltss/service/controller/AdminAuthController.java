package ltss.service.controller;

import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;
import ltss.service.dto.admin.AdminLoginRequest;
import ltss.service.dto.admin.AdminLoginResponse;
import ltss.service.service.AdminAuthService;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

@RestController
@RequiredArgsConstructor
@RequestMapping("/api/admin/auth")
public class AdminAuthController {

    private final AdminAuthService adminAuthService;

    @PostMapping("/login")
    public AdminLoginResponse login(@Valid @RequestBody AdminLoginRequest request) {
        return adminAuthService.login(request);
    }
}
