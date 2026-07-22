package com.mnemosyne.backend.save;

import com.mnemosyne.backend.storage.ObjectStorageService;
import com.mnemosyne.backend.user.User;
import com.mnemosyne.backend.user.UserRepository;
import jakarta.validation.Valid;
import org.springframework.http.HttpStatus;
import org.springframework.http.ResponseEntity;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.security.core.userdetails.UserDetails;
import org.springframework.web.bind.annotation.*;

import java.util.List;

@RestController
@RequestMapping("/saves")
public class SaveController {

    private final SaveService saveService;
    private final UserRepository userRepository;
    private final ObjectStorageService objectStorageService;

    public SaveController(SaveService saveService,
                          UserRepository userRepository,
                          ObjectStorageService objectStorageService) {
        this.saveService = saveService;
        this.userRepository = userRepository;
        this.objectStorageService = objectStorageService;
    }

    @PostMapping
    public ResponseEntity<SaveResponse> create(@Valid @RequestBody SaveRequest request,
                                               @AuthenticationPrincipal UserDetails principal) {
        User user = userRepository.findByEmail(principal.getUsername());
        Save save = saveService.create(user, request.type(), request.sourceUrl(), request.imageRef());
        return ResponseEntity.status(HttpStatus.CREATED).body(SaveResponse.from(save, objectStorageService));
    }

    @GetMapping
    public List<SaveResponse> list(@RequestParam(required = false) String status,
                                   @AuthenticationPrincipal UserDetails principal) {
        User user = userRepository.findByEmail(principal.getUsername());
        return saveService.listForUser(user, status).stream()
                .map(s -> SaveResponse.from(s, objectStorageService))
                .toList();
    }

    @GetMapping("/{id}")
    public ResponseEntity<SaveResponse> get(@PathVariable Long id,
                                            @AuthenticationPrincipal UserDetails principal) {
        Save save = saveService.findByIdForUser(id, principal.getUsername());
        return save != null
                ? ResponseEntity.ok(SaveResponse.from(save, objectStorageService))
                : ResponseEntity.notFound().build();
    }

    @PostMapping("/{id}/complete")
    public ResponseEntity<Void> complete(@PathVariable Long id,
                                         @AuthenticationPrincipal UserDetails principal) {
        return saveService.complete(id, principal.getUsername())
                ? ResponseEntity.ok().build()
                : ResponseEntity.notFound().build();
    }

    @PostMapping("/{id}/archive")
    public ResponseEntity<Void> archive(@PathVariable Long id,
                                        @AuthenticationPrincipal UserDetails principal) {
        return saveService.archive(id, principal.getUsername())
                ? ResponseEntity.ok().build()
                : ResponseEntity.notFound().build();
    }

    @PostMapping("/{id}/uncomplete")
    public ResponseEntity<Void> uncomplete(@PathVariable Long id,
                                           @AuthenticationPrincipal UserDetails principal) {
        return saveService.uncomplete(id, principal.getUsername())
                ? ResponseEntity.ok().build()
                : ResponseEntity.notFound().build();
    }

    @PostMapping("/{id}/unarchive")
    public ResponseEntity<Void> unarchive(@PathVariable Long id,
                                          @AuthenticationPrincipal UserDetails principal) {
        return saveService.unarchive(id, principal.getUsername())
                ? ResponseEntity.ok().build()
                : ResponseEntity.notFound().build();
    }

    @PutMapping("/{id}/category")
    public ResponseEntity<SaveResponse> updateCategory(@PathVariable Long id,
                                                       @Valid @RequestBody CategoryUpdateRequest request,
                                                       @AuthenticationPrincipal UserDetails principal) {
        Save save = saveService.updateCategory(id, principal.getUsername(), request.category());
        return save != null
                ? ResponseEntity.ok(SaveResponse.from(save, objectStorageService))
                : ResponseEntity.notFound().build();
    }

    @DeleteMapping("/{id}")
    public ResponseEntity<Void> delete(@PathVariable Long id,
                                       @AuthenticationPrincipal UserDetails principal) {
        return saveService.delete(id, principal.getUsername())
                ? ResponseEntity.noContent().build()
                : ResponseEntity.notFound().build();
    }

    @DeleteMapping("/categories")
    public ResponseEntity<Void> deleteCategory(@RequestParam String category,
                                               @AuthenticationPrincipal UserDetails principal) {
        User user = userRepository.findByEmail(principal.getUsername());
        saveService.deleteCategory(user, category);
        return ResponseEntity.noContent().build();
    }
}
