package com.mnemosyne.backend.user;

import jakarta.persistence.Entity;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.Table;
import jakarta.persistence.Column;
import jakarta.persistence.PrePersist;
import java.time.LocalDateTime;

@Entity
@Table(name = "users")
public class User {

    public Long getId() {
        return id;
    }

    public void setId(Long id) {
        this.id = id;
    }

    public String getPasswordHash() {
        return passwordHash;
    }

    public void setPasswordHash(String passwordHash) {
        this.passwordHash = passwordHash;
    }

    public String getEmail() {
        return email;
    }

    public void setEmail(String email) {
        this.email = email;
    }

    public LocalDateTime getCreatedAt() {
        return createdAt;
    }

    public void setCreatedAt(LocalDateTime createdAt) {
        this.createdAt = createdAt;
    }

    public String getDisplayName() {
        return displayName;
    }

    public void setDisplayName(String displayName) {
        this.displayName = displayName;
    }

    public boolean isDailyDigestEnabled() {
        return dailyDigestEnabled;
    }

    public void setDailyDigestEnabled(boolean dailyDigestEnabled) {
        this.dailyDigestEnabled = dailyDigestEnabled;
    }

    public String getDigestTime() {
        return digestTime;
    }

    public void setDigestTime(String digestTime) {
        this.digestTime = digestTime;
    }

    public boolean isNotificationsEnabled() {
        return notificationsEnabled;
    }

    public void setNotificationsEnabled(boolean notificationsEnabled) {
        this.notificationsEnabled = notificationsEnabled;
    }

    public int getAutoArchiveDays() {
        return autoArchiveDays;
    }

    public void setAutoArchiveDays(int autoArchiveDays) {
        this.autoArchiveDays = autoArchiveDays;
    }

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(name = "password_hash", nullable = false)
    private String passwordHash;

    @Column(name = "email", nullable = false, unique = true)
    private String email;

    @Column(name="created_at", nullable = false)
    private LocalDateTime createdAt;

    @Column(name = "display_name", length = 80)
    private String displayName;

    @Column(name = "daily_digest_enabled", nullable = false)
    private boolean dailyDigestEnabled = true;

    @Column(name = "digest_time", nullable = false, length = 5)
    private String digestTime = "07:00";

    @Column(name = "notifications_enabled", nullable = false)
    private boolean notificationsEnabled = true;

    @Column(name = "auto_archive_days", nullable = false)
    private int autoArchiveDays = 30;

    @PrePersist
    protected void onCreate() {
        this.createdAt = LocalDateTime.now();
        if (this.displayName == null || this.displayName.isBlank()) {
            this.displayName = defaultDisplayName(this.email);
        }
    }

    private static String defaultDisplayName(String email) {
        if (email == null || email.isBlank()) return "Muninn User";

        String local = email.split("@", 2)[0]
                .replace('.', ' ')
                .replace('_', ' ')
                .replace('-', ' ')
                .trim();

        if (local.isBlank()) return email;

        StringBuilder result = new StringBuilder();
        for (String part : local.split("\\s+")) {
            if (part.isBlank()) continue;
            if (!result.isEmpty()) result.append(' ');
            result.append(Character.toUpperCase(part.charAt(0)));
            if (part.length() > 1) result.append(part.substring(1).toLowerCase());
        }
        return result.isEmpty() ? email : result.toString();
    }

}
