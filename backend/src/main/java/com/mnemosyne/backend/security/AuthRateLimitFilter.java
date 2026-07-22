package com.mnemosyne.backend.security;

import jakarta.servlet.FilterChain;
import jakarta.servlet.ServletException;
import jakarta.servlet.http.HttpServletRequest;
import jakarta.servlet.http.HttpServletResponse;
import org.springframework.stereotype.Component;
import org.springframework.web.filter.OncePerRequestFilter;

import java.io.IOException;
import java.time.Duration;
import java.util.Map;
import java.util.concurrent.ConcurrentHashMap;

@Component
public class AuthRateLimitFilter extends OncePerRequestFilter {

    private static final int MAX_ATTEMPTS_PER_WINDOW = 8;
    private static final long WINDOW_MILLIS = Duration.ofMinutes(1).toMillis();
    private static final long STALE_WINDOW_MILLIS = Duration.ofMinutes(5).toMillis();

    private final Map<String, AttemptWindow> attemptsByClient = new ConcurrentHashMap<>();

    @Override
    protected void doFilterInternal(HttpServletRequest request,
                                    HttpServletResponse response,
                                    FilterChain filterChain) throws ServletException, IOException {
        if (!isLimitedAuthRequest(request)) {
            filterChain.doFilter(request, response);
            return;
        }

        long now = System.currentTimeMillis();
        cleanupStaleWindows(now);

        String key = request.getServletPath() + ":" + clientIp(request);
        AttemptWindow window = attemptsByClient.computeIfAbsent(key, ignored -> new AttemptWindow(now));

        if (!window.tryAcquire(now)) {
            response.setStatus(429);
            response.setHeader("Retry-After", String.valueOf(WINDOW_MILLIS / 1000));
            response.setContentType("application/json");
            response.getWriter().write("{\"status\":429,\"error\":\"Too many authentication attempts. Please try again later.\"}");
            return;
        }

        filterChain.doFilter(request, response);
    }

    private static boolean isLimitedAuthRequest(HttpServletRequest request) {
        if (!"POST".equalsIgnoreCase(request.getMethod())) {
            return false;
        }

        String path = request.getServletPath();
        return "/auth/login".equals(path) || "/auth/register".equals(path);
    }

    private static String clientIp(HttpServletRequest request) {
        String forwardedFor = request.getHeader("X-Forwarded-For");
        if (forwardedFor != null && !forwardedFor.isBlank()) {
            return forwardedFor.split(",", 2)[0].trim();
        }

        return request.getRemoteAddr();
    }

    private void cleanupStaleWindows(long now) {
        attemptsByClient.entrySet()
                .removeIf(entry -> now - entry.getValue().windowStartMillis() > STALE_WINDOW_MILLIS);
    }

    private static final class AttemptWindow {
        private long windowStartMillis;
        private int attempts;

        private AttemptWindow(long windowStartMillis) {
            this.windowStartMillis = windowStartMillis;
        }

        private synchronized boolean tryAcquire(long now) {
            if (now - windowStartMillis >= WINDOW_MILLIS) {
                windowStartMillis = now;
                attempts = 0;
            }

            if (attempts >= MAX_ATTEMPTS_PER_WINDOW) {
                return false;
            }

            attempts++;
            return true;
        }

        private synchronized long windowStartMillis() {
            return windowStartMillis;
        }
    }
}
