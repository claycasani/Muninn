package com.mnemosyne.backend.category;

import com.mnemosyne.backend.user.User;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

import java.util.List;
import java.util.Optional;

public interface CategoryRepository extends JpaRepository<Category, Long> {

    List<Category> findByUserOrderBySortOrderAscNameAsc(User user);

    Optional<Category> findByUserAndNameIgnoreCase(User user, String name);

    boolean existsByUserAndNameIgnoreCase(User user, String name);

    long countByUser(User user);

    /** Category names for the AI vocabulary, ordered — fetched by id so it is safe
        inside the async analysis path where the User association may be a proxy. */
    @Query("select c.name from Category c where c.user.id = :userId "
            + "order by c.sortOrder asc, c.name asc")
    List<String> findNamesByUserId(@Param("userId") Long userId);

    @Query("select coalesce(max(c.sortOrder), -1) from Category c where c.user = :user")
    int maxSortOrder(@Param("user") User user);
}
