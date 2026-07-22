package com.mnemosyne.backend.config;

import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.MethodArgumentNotValidException;
import org.springframework.web.bind.annotation.ExceptionHandler;
import org.springframework.web.bind.annotation.RestControllerAdvice;
import org.springframework.http.converter.HttpMessageNotReadableException;
import org.springframework.web.server.ResponseStatusException;

import java.util.Map;
import java.util.stream.Collectors;

/**
 * Returns structured JSON for validation errors instead of Spring's default HTML error page.
 * All field constraint violations are joined into a single human-readable message.
 */
@RestControllerAdvice
public class GlobalExceptionHandler {

    @ExceptionHandler(MethodArgumentNotValidException.class)
    public ResponseEntity<Map<String, Object>> handleValidation(MethodArgumentNotValidException ex) {
        String message = ex.getBindingResult().getFieldErrors().stream()
                .map(e -> friendlyMessage(e.getField(), e.getDefaultMessage()))
                .collect(Collectors.joining(" · "));

        return ResponseEntity.badRequest()
                .body(Map.of("status", 400, "error", message));
    }

    @ExceptionHandler(HttpMessageNotReadableException.class)
    public ResponseEntity<Map<String, Object>> handleMalformedRequest(HttpMessageNotReadableException ex) {
        return ResponseEntity.badRequest()
                .body(Map.of("status", 400, "error", "Malformed request."));
    }

    // Deliberate status exceptions (e.g. category conflicts/not-found) must keep
    // their status and reason and NOT be swallowed by the generic 500 handler below.
    @ExceptionHandler(ResponseStatusException.class)
    public ResponseEntity<Map<String, Object>> handleResponseStatus(ResponseStatusException ex) {
        int status = ex.getStatusCode().value();
        String reason = ex.getReason() != null ? ex.getReason() : "Request failed.";
        return ResponseEntity.status(status).body(Map.of("status", status, "error", reason));
    }

    @ExceptionHandler(Exception.class)
    public ResponseEntity<Map<String, Object>> handleUnexpected(Exception ex) {
        return ResponseEntity.internalServerError()
                .body(Map.of("status", 500, "error", "An unexpected error occurred."));
    }

    private static String friendlyMessage(String field, String constraint) {
        return switch (field) {
            case "password" -> "Password must be at least 8 characters.";
            case "email"    -> "Please enter a valid email address.";
            default         -> field + ": " + constraint;
        };
    }
}
