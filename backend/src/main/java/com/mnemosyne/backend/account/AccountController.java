package com.mnemosyne.backend.account;

import com.mnemosyne.backend.save.SaveRepository;
import com.mnemosyne.backend.user.User;
import com.mnemosyne.backend.user.UserRepository;
import jakarta.validation.Valid;
import org.springframework.http.HttpStatus;
import org.springframework.http.ResponseEntity;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.security.core.userdetails.UserDetails;
import org.springframework.security.crypto.password.PasswordEncoder;
import org.springframework.transaction.annotation.Transactional;
import org.springframework.web.bind.annotation.DeleteMapping;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.PutMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

import java.util.Set;
import java.util.regex.Pattern;

@RestController
@RequestMapping("/account")
public class AccountController {

    private static final Pattern DIGEST_TIME_PATTERN = Pattern.compile("^([01]\\d|2[0-3]):[0-5]\\d$");
    private static final Set<Integer> AUTO_ARCHIVE_OPTIONS = Set.of(7, 14, 30, 60, 90);

    private final UserRepository userRepository;
    private final SaveRepository saveRepository;
    private final PasswordEncoder passwordEncoder;

    public AccountController(UserRepository userRepository,
                             SaveRepository saveRepository,
                             PasswordEncoder passwordEncoder) {
        this.userRepository = userRepository;
        this.saveRepository = saveRepository;
        this.passwordEncoder = passwordEncoder;
    }

    @GetMapping
    public ResponseEntity<AccountResponse> getAccount(@AuthenticationPrincipal UserDetails principal) {
        User user = userRepository.findByEmail(principal.getUsername());
        if (user == null) return ResponseEntity.notFound().build();
        return ResponseEntity.ok(toResponse(user));
    }

    @PutMapping
    public ResponseEntity<AccountResponse> updateAccount(
            @AuthenticationPrincipal UserDetails principal,
            @RequestBody UpdateAccountRequest request) {
        User user = userRepository.findByEmail(principal.getUsername());
        if (user == null) return ResponseEntity.notFound().build();

        if (request.displayName() != null) {
            String displayName = request.displayName().trim();
            if (displayName.isBlank() || displayName.length() > 80) {
                return ResponseEntity.badRequest().build();
            }
            user.setDisplayName(displayName);
        }

        if (request.dailyDigestEnabled() != null) {
            user.setDailyDigestEnabled(request.dailyDigestEnabled());
        }

        if (request.digestTime() != null) {
            String digestTime = request.digestTime().trim();
            if (!DIGEST_TIME_PATTERN.matcher(digestTime).matches()) {
                return ResponseEntity.badRequest().build();
            }
            user.setDigestTime(digestTime);
        }

        if (request.notificationsEnabled() != null) {
            user.setNotificationsEnabled(request.notificationsEnabled());
        }

        if (request.autoArchiveDays() != null) {
            if (!AUTO_ARCHIVE_OPTIONS.contains(request.autoArchiveDays())) {
                return ResponseEntity.badRequest().build();
            }
            user.setAutoArchiveDays(request.autoArchiveDays());
        }

        userRepository.save(user);
        return ResponseEntity.ok(toResponse(user));
    }

    @PostMapping("/password")
    public ResponseEntity<Void> changePassword(
            @AuthenticationPrincipal UserDetails principal,
            @Valid @RequestBody ChangePasswordRequest request) {
        User user = userRepository.findByEmail(principal.getUsername());
        if (user == null) return ResponseEntity.notFound().build();

        if (!passwordEncoder.matches(request.currentPassword(), user.getPasswordHash())) {
            return ResponseEntity.status(HttpStatus.FORBIDDEN).build();
        }

        user.setPasswordHash(passwordEncoder.encode(request.newPassword()));
        userRepository.save(user);
        return ResponseEntity.noContent().build();
    }

    @DeleteMapping
    @Transactional
    public ResponseEntity<Void> deleteAccount(@AuthenticationPrincipal UserDetails principal) {
        User user = userRepository.findByEmail(principal.getUsername());
        if (user == null) return ResponseEntity.notFound().build();

        saveRepository.deleteByUser(user);
        userRepository.delete(user);
        return ResponseEntity.noContent().build();
    }

    private static AccountResponse toResponse(User user) {
        return new AccountResponse(
                user.getEmail(),
                user.getDisplayName(),
                user.isDailyDigestEnabled(),
                user.getDigestTime(),
                user.isNotificationsEnabled(),
                user.getAutoArchiveDays()
        );
    }
}
