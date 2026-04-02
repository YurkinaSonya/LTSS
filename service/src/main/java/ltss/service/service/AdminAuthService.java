package ltss.service.service;

import lombok.RequiredArgsConstructor;
import ltss.service.dto.admin.AdminLoginRequest;
import ltss.service.dto.admin.AdminLoginResponse;
import ltss.service.entity.AdminUser;
import ltss.service.exception.UnauthorizedException;
import ltss.service.repository.AdminUserRepository;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

@Service
@RequiredArgsConstructor
public class AdminAuthService {

    private final AdminUserRepository adminUserRepository;
    private final PasswordService passwordService;
    private final AdminTokenService adminTokenService;

    @Transactional(readOnly = true)
    public AdminLoginResponse login(AdminLoginRequest request) {
        AdminUser adminUser = adminUserRepository.findByUsername(request.username())
                .orElseThrow(() -> new UnauthorizedException("Invalid admin username or password"));

        if (!adminUser.isActive() || !passwordService.matches(request.password(), adminUser.getPasswordHash())) {
            throw new UnauthorizedException("Invalid admin username or password");
        }

        return new AdminLoginResponse(
                adminTokenService.createToken(adminUser.getId(), adminUser.getUsername()),
                new AdminLoginResponse.AdminUserResponse(
                        adminUser.getId(),
                        adminUser.getUsername(),
                        adminUser.getRole()
                )
        );
    }
}
