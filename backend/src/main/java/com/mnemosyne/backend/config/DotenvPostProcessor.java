package com.mnemosyne.backend.config;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.boot.SpringApplication;
import org.springframework.boot.env.EnvironmentPostProcessor;
import org.springframework.core.Ordered;
import org.springframework.core.annotation.Order;
import org.springframework.core.env.ConfigurableEnvironment;
import org.springframework.core.env.MapPropertySource;

import java.io.BufferedReader;
import java.io.FileReader;
import java.nio.file.Path;
import java.util.LinkedHashMap;
import java.util.Map;

/**
 * Loads a {@code .env} file from the working directory into Spring's Environment
 * before any beans are created.
 *
 * <p>Rules:
 * <ul>
 *   <li>File is optional — silently no-ops if absent (production servers use real env vars).</li>
 *   <li>Real OS environment variables always win: .env values are added at the lowest priority.</li>
 *   <li>YAML / application.properties values that use {@code ${VAR:}} placeholders resolve against
 *       these properties since the source is visible to Spring's placeholder resolver.</li>
 *   <li>Lines starting with {@code #} and blank lines are ignored.</li>
 *   <li>Values may be quoted with {@code "} or {@code '} — quotes are stripped.</li>
 *   <li>Inline comments (e.g. {@code KEY=value # comment}) are NOT stripped — keep .env
 *       values clean.</li>
 * </ul>
 *
 * <p>Registered via {@code META-INF/spring/org.springframework.boot.env.EnvironmentPostProcessor.imports}
 * which is the Spring Boot 3+/4+ mechanism (replaces the old {@code spring.factories} approach).
 */
@Order(Ordered.LOWEST_PRECEDENCE)
public class DotenvPostProcessor implements EnvironmentPostProcessor {

    private static final Logger log = LoggerFactory.getLogger(DotenvPostProcessor.class);

    @Override
    public void postProcessEnvironment(ConfigurableEnvironment environment, SpringApplication application) {
        Path envFile = Path.of(System.getProperty("user.dir"), ".env");
        if (!envFile.toFile().exists()) {
            // Normal in production — no .env present, real env vars are in use
            return;
        }

        Map<String, Object> properties = new LinkedHashMap<>();
        try (BufferedReader reader = new BufferedReader(new FileReader(envFile.toFile()))) {
            String line;
            int lineNum = 0;
            while ((line = reader.readLine()) != null) {
                lineNum++;
                line = line.strip();
                if (line.isEmpty() || line.startsWith("#")) continue;

                int eqIdx = line.indexOf('=');
                if (eqIdx < 0) {
                    log.debug(".env line {} has no '=' — skipped", lineNum);
                    continue;
                }

                String key = line.substring(0, eqIdx).strip();
                String value = line.substring(eqIdx + 1).strip();

                // Strip matching outer quotes
                if (value.length() >= 2 &&
                    ((value.charAt(0) == '"' && value.charAt(value.length() - 1) == '"') ||
                     (value.charAt(0) == '\'' && value.charAt(value.length() - 1) == '\''))) {
                    value = value.substring(1, value.length() - 1);
                }

                // Real env vars (already in the environment) always take precedence
                if (!environment.containsProperty(key)) {
                    properties.put(key, value);
                }
            }
        } catch (Exception e) {
            log.warn("Could not read .env file at '{}': {}", envFile, e.getMessage());
            return;
        }

        if (!properties.isEmpty()) {
            // addLast = lowest priority: real env vars and YAML system props win
            environment.getPropertySources().addLast(new MapPropertySource("dotenv", properties));
        }
    }
}
