package com.mnemosyne.backend.account;

public record UpdateAccountRequest(
        String displayName,
        Boolean dailyDigestEnabled,
        String digestTime,
        Boolean notificationsEnabled,
        Integer autoArchiveDays
) {}
