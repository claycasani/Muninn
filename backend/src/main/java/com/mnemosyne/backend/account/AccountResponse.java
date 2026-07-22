package com.mnemosyne.backend.account;

public record AccountResponse(
        String email,
        String displayName,
        boolean dailyDigestEnabled,
        String digestTime,
        boolean notificationsEnabled,
        int autoArchiveDays
) {}
