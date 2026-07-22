package com.mnemosyne.backend.category;

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
@RequestMapping("/categories")
public class CategoryController {

    private final CategoryService categoryService;
    private final UserRepository userRepository;

    public CategoryController(CategoryService categoryService, UserRepository userRepository) {
        this.categoryService = categoryService;
        this.userRepository = userRepository;
    }

    @GetMapping
    public List<CategoryResponse> list(@AuthenticationPrincipal UserDetails principal) {
        return categoryService.list(user(principal)).stream()
                .map(CategoryResponse::from)
                .toList();
    }

    @PostMapping
    public ResponseEntity<CategoryResponse> create(@Valid @RequestBody CategoryNameRequest request,
                                                   @AuthenticationPrincipal UserDetails principal) {
        Category created = categoryService.create(user(principal), request.name());
        return ResponseEntity.status(HttpStatus.CREATED).body(CategoryResponse.from(created));
    }

    @PutMapping("/{id}")
    public CategoryResponse rename(@PathVariable Long id,
                                   @Valid @RequestBody CategoryNameRequest request,
                                   @AuthenticationPrincipal UserDetails principal) {
        return CategoryResponse.from(categoryService.rename(user(principal), id, request.name()));
    }

    @DeleteMapping("/{id}")
    public ResponseEntity<Void> delete(@PathVariable Long id,
                                       @AuthenticationPrincipal UserDetails principal) {
        categoryService.delete(user(principal), id);
        return ResponseEntity.noContent().build();
    }

    private User user(UserDetails principal) {
        return userRepository.findByEmail(principal.getUsername());
    }
}
