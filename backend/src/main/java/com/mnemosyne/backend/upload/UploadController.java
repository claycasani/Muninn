package com.mnemosyne.backend.upload;

import com.mnemosyne.backend.storage.ObjectStorageService;
import com.mnemosyne.backend.user.User;
import com.mnemosyne.backend.user.UserRepository;
import org.springframework.http.ResponseEntity;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.security.core.userdetails.UserDetails;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.RestController;

import java.time.Duration;
import java.util.Map;
import java.util.UUID;

/**
 * Handles direct-to-R2 upload flow.
 *
 * Flow:
 *   1. Client calls GET /uploads/presign?contentType=image/png  →  { key, uploadUrl }
 *   2. Client PUTs raw bytes to uploadUrl with matching Content-Type header
 *   3. Client calls POST /saves with { type: "image", imageRef: key }
 */
@RestController
@RequestMapping("/uploads")
public class UploadController {

    private final ObjectStorageService objectStorageService;
    private final UserRepository userRepository;

    public UploadController(ObjectStorageService objectStorageService, UserRepository userRepository) {
        this.objectStorageService = objectStorageService;
        this.userRepository = userRepository;
    }

    /**
     * Returns a presigned PUT URL the client uses to upload an image directly to R2.
     *
     * @param contentType MIME type of the image (default: image/png — both Mac + iOS screenshots are PNG)
     */
    @GetMapping("/presign")
    public ResponseEntity<?> presign(
            @RequestParam(defaultValue = "image/png") String contentType,
            @AuthenticationPrincipal UserDetails principal) {

        User user = userRepository.findByEmail(principal.getUsername());
        String ext = extensionFor(contentType);
        String key = "images/" + user.getId() + "/" + UUID.randomUUID() + "." + ext;

        String uploadUrl = objectStorageService.generatePresignedPutUrl(key, contentType, Duration.ofMinutes(15));
        if (uploadUrl == null) {
            return ResponseEntity.internalServerError()
                    .body(Map.of("error", "Object storage is not configured on this server"));
        }

        return ResponseEntity.ok(Map.of("key", key, "uploadUrl", uploadUrl));
    }

    /** Maps a MIME type to the file extension encoded in the R2 key. */
    private static String extensionFor(String contentType) {
        return switch (contentType.toLowerCase()) {
            case "image/jpeg" -> "jpg";
            case "image/heic" -> "heic";
            case "image/webp" -> "webp";
            default -> "png";   // image/png and unknowns → .png
        };
    }
}
