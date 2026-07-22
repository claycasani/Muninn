package com.mnemosyne.backend.storage;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Service;
import software.amazon.awssdk.auth.credentials.AwsBasicCredentials;
import software.amazon.awssdk.auth.credentials.StaticCredentialsProvider;
import software.amazon.awssdk.core.ResponseBytes;
import software.amazon.awssdk.regions.Region;
import software.amazon.awssdk.services.s3.S3Client;
import software.amazon.awssdk.services.s3.S3Configuration;
import software.amazon.awssdk.services.s3.model.GetObjectRequest;
import software.amazon.awssdk.services.s3.model.GetObjectResponse;
import software.amazon.awssdk.services.s3.model.DeleteObjectRequest;
import software.amazon.awssdk.services.s3.presigner.S3Presigner;
import software.amazon.awssdk.services.s3.presigner.model.GetObjectPresignRequest;
import software.amazon.awssdk.services.s3.presigner.model.PresignedGetObjectRequest;
import software.amazon.awssdk.services.s3.presigner.model.PresignedPutObjectRequest;
import software.amazon.awssdk.services.s3.presigner.model.PutObjectPresignRequest;

import java.net.URI;
import java.time.Duration;

/**
 * R2-backed object storage via the AWS S3-compatible API.
 *
 * Gracefully degrades: if R2 env vars are not set, all operations log a warning
 * and return null / empty so the rest of the app keeps running locally without R2.
 */
@Service
public class ObjectStorageService {

    private static final Logger log = LoggerFactory.getLogger(ObjectStorageService.class);

    private final String bucketName;
    private final S3Client s3Client;
    private final S3Presigner s3Presigner;
    private final boolean enabled;

    public ObjectStorageService(
            @Value("${r2.account-id:}") String accountId,
            @Value("${r2.access-key-id:}") String accessKeyId,
            @Value("${r2.secret-access-key:}") String secretAccessKey,
            @Value("${r2.bucket-name:}") String bucketName) {

        this.bucketName = bucketName;

        if (accountId.isBlank() || accessKeyId.isBlank() || secretAccessKey.isBlank() || bucketName.isBlank()) {
            log.warn("R2 credentials not configured — ObjectStorageService disabled. " +
                     "Set R2_ACCOUNT_ID, R2_ACCESS_KEY_ID, R2_SECRET_ACCESS_KEY, R2_BUCKET_NAME to enable image saves.");
            this.s3Client = null;
            this.s3Presigner = null;
            this.enabled = false;
            return;
        }

        URI endpoint = URI.create("https://" + accountId + ".r2.cloudflarestorage.com");
        StaticCredentialsProvider credentials = StaticCredentialsProvider.create(
                AwsBasicCredentials.create(accessKeyId, secretAccessKey));

        // R2 is S3-compatible. Use US_EAST_1 for signing (R2 ignores the region).
        // Path-style addressing is required when using a custom endpoint override.
        S3Configuration s3Config = S3Configuration.builder()
                .pathStyleAccessEnabled(true)
                .build();

        this.s3Client = S3Client.builder()
                .endpointOverride(endpoint)
                .credentialsProvider(credentials)
                .region(Region.US_EAST_1)
                .serviceConfiguration(s3Config)
                .build();

        this.s3Presigner = S3Presigner.builder()
                .endpointOverride(endpoint)
                .credentialsProvider(credentials)
                .region(Region.US_EAST_1)
                .serviceConfiguration(s3Config)
                .build();

        this.enabled = true;
        log.info("ObjectStorageService connected to R2 bucket '{}'", bucketName);
    }

    /**
     * Generates a presigned PUT URL valid for {@code ttl}.
     * The client should PUT the raw image bytes to this URL (no auth headers needed),
     * then POST /saves with the returned key as imageRef.
     *
     * @return the presigned URL string, or null if R2 is not configured
     */
    public String generatePresignedPutUrl(String key, String contentType, Duration ttl) {
        if (!enabled) {
            log.warn("R2 not configured — cannot generate presigned URL for '{}'", key);
            return null;
        }
        try {
            PutObjectPresignRequest presignRequest = PutObjectPresignRequest.builder()
                    .signatureDuration(ttl)
                    .putObjectRequest(r -> r
                            .bucket(bucketName)
                            .key(key)
                            .contentType(contentType))
                    .build();

            PresignedPutObjectRequest presigned = s3Presigner.presignPutObject(presignRequest);
            return presigned.url().toString();
        } catch (Exception e) {
            log.error("Failed to generate presigned URL for '{}': {}", key, e.getMessage(), e);
            return null;
        }
    }

    /**
     * Generates a presigned GET URL valid for {@code ttl}.
     * Use this to produce short-lived thumbnail URLs to embed in API responses —
     * the client can render them directly without any auth header.
     *
     * @return the presigned URL string, or null if R2 is not configured or key is blank
     */
    public String generatePresignedGetUrl(String key, Duration ttl) {
        if (!enabled) {
            log.warn("R2 not configured — cannot generate presigned GET URL for '{}'", key);
            return null;
        }
        if (key == null || key.isBlank()) return null;
        try {
            GetObjectPresignRequest presignRequest = GetObjectPresignRequest.builder()
                    .signatureDuration(ttl)
                    .getObjectRequest(r -> r
                            .bucket(bucketName)
                            .key(key))
                    .build();

            PresignedGetObjectRequest presigned = s3Presigner.presignGetObject(presignRequest);
            return presigned.url().toString();
        } catch (Exception e) {
            log.error("Failed to generate presigned GET URL for '{}': {}", key, e.getMessage(), e);
            return null;
        }
    }

    /**
     * Fetches and returns the raw bytes for the given object key.
     *
     * @return the bytes, or null if R2 is not configured or the fetch fails
     */
    public byte[] getObjectBytes(String key) {
        if (!enabled) {
            log.warn("R2 not configured — cannot fetch '{}'", key);
            return null;
        }
        try {
            GetObjectRequest request = GetObjectRequest.builder()
                    .bucket(bucketName)
                    .key(key)
                    .build();
            ResponseBytes<GetObjectResponse> response = s3Client.getObjectAsBytes(request);
            return response.asByteArray();
        } catch (Exception e) {
            log.error("Failed to fetch '{}' from R2: {}", key, e.getMessage(), e);
            return null;
        }
    }

    /**
     * Deletes the object for the given key.
     *
     * @return true when the delete request succeeds or the key is blank; false when R2 is
     * not configured or the delete call throws.
     */
    public boolean deleteObject(String key) {
        if (key == null || key.isBlank()) return true;
        if (!enabled) {
            log.warn("R2 not configured — cannot delete '{}'", key);
            return false;
        }
        try {
            DeleteObjectRequest request = DeleteObjectRequest.builder()
                    .bucket(bucketName)
                    .key(key)
                    .build();
            s3Client.deleteObject(request);
            return true;
        } catch (Exception e) {
            log.error("Failed to delete '{}' from R2: {}", key, e.getMessage(), e);
            return false;
        }
    }
}
