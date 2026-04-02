package ltss.service.service;

import jakarta.servlet.http.HttpServletRequest;
import lombok.RequiredArgsConstructor;
import ltss.service.entity.AdminUser;
import ltss.service.exception.UnauthorizedException;
import ltss.service.repository.AdminUserRepository;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

@Service
@RequiredArgsConstructor
public class AdminAccessService {

    public static final String ADMIN_USER_REQUEST_ATTRIBUTE = "ltss.adminUser";

    private final AdminUserRepository adminUserRepository;
    private final AdminTokenService adminTokenService;

    @Transactional(readOnly = true)
    public AdminUser getAuthorizedAdmin(HttpServletRequest request) {
        Object cached = request.getAttribute(ADMIN_USER_REQUEST_ATTRIBUTE);
        if (cached instanceof AdminUser adminUser) {
            return adminUser;
        }

        AdminTokenService.AdminTokenPayload payload = adminTokenService.parseToken(extractToken(request));
        AdminUser adminUser = adminUserRepository.findById(payload.adminUserId())
                .orElseThrow(() -> new UnauthorizedException("Admin token is no longer valid"));

        if (!adminUser.isActive() || !adminUser.getUsername().equals(payload.username())) {
            throw new UnauthorizedException("Admin token is no longer valid");
        }

        request.setAttribute(ADMIN_USER_REQUEST_ATTRIBUTE, adminUser);
        return adminUser;
    }

    private String extractToken(HttpServletRequest request) {
        String authorization = request.getHeader("Authorization");
        if (authorization != null && authorization.startsWith("Bearer ")) {
            return authorization.substring(7);
        }
        return request.getHeader("X-Admin-Token");
    }
}
