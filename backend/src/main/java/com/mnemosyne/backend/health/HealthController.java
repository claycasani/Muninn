package com.mnemosyne.backend.health;

import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RestController;

import java.util.Map;

/**
 * Small unauthenticated liveness endpoint for the hosting platform.
 *
 * This intentionally checks only that the web process is serving requests. Database
 * connectivity is exercised by Flyway and JPA during startup, while keeping the
 * health check itself cheap and safe to expose publicly.
 */
@RestController
public class HealthController {

    @GetMapping("/health")
    public Map<String, String> health() {
        return Map.of("status", "ok");
    }
}
