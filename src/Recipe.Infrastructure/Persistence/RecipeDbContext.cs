using Microsoft.EntityFrameworkCore;
using Recipe.Domain;
using RecipeEntity = Recipe.Domain.Recipe;

namespace Recipe.Infrastructure.Persistence;

public sealed class RecipeDbContext(DbContextOptions<RecipeDbContext> options) : DbContext(options)
{
    public DbSet<RecipeEntity> Recipes => Set<RecipeEntity>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<RecipeType> RecipeTypes => Set<RecipeType>();
    public DbSet<Tag> Tags => Set<Tag>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        // Explicit ICU collation keeps Swedish case-insensitive search consistent across host locales.
        model.HasCollation("recipe_text", locale: "und", provider: "icu", deterministic: true);
        model.Entity<User>().Property(x => x.DisplayName).HasMaxLength(200);
        var recipe = model.Entity<RecipeEntity>();
        recipe.ToTable("Recipes", t =>
        {
            t.HasCheckConstraint("CK_Recipe_Yield", "(\"YieldValue\" IS NULL AND \"YieldMaximumValue\" IS NULL AND \"YieldUnitId\" IS NULL) OR (\"YieldValue\" IS NOT NULL AND \"YieldValue\" > 0 AND \"YieldUnitId\" IS NOT NULL AND (\"YieldMaximumValue\" IS NULL OR \"YieldMaximumValue\" > \"YieldValue\"))");
            t.HasCheckConstraint("CK_Recipe_Time", "\"TotalTimeMinutes\" IS NULL OR \"TotalTimeMinutes\" > 0");
        });
        recipe.Property(x => x.Name).HasMaxLength(200).UseCollation("recipe_text");
        recipe.Property(x => x.PublicationState).HasConversion<string>();
        recipe.HasOne<User>().WithMany().HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Restrict);
        recipe.HasOne<RecipeType>().WithMany().HasForeignKey(x => x.RecipeTypeId).OnDelete(DeleteBehavior.Restrict);
        recipe.OwnsOne(x => x.Yield, y =>
        {
            y.Property(x => x.UnitId).HasColumnName("YieldUnitId");
            y.HasOne<Unit>().WithMany().HasForeignKey(x => x.UnitId).OnDelete(DeleteBehavior.Restrict);
            y.OwnsOne(x => x.Quantity, q =>
            {
                q.Property(x => x.Value).HasColumnName("YieldValue").IsRequired();
                q.Property(x => x.MaximumValue).HasColumnName("YieldMaximumValue");
            });
            y.Navigation(x => x.Quantity).IsRequired();
        });
        recipe.Navigation(x => x.Yield).IsRequired(false);
        recipe.HasMany(x => x.IngredientGroups).WithOne().HasForeignKey(x => x.RecipeId).OnDelete(DeleteBehavior.Cascade);
        recipe.HasMany(x => x.CookingSteps).WithOne().HasForeignKey(x => x.RecipeId).OnDelete(DeleteBehavior.Cascade);
        recipe.HasMany(x => x.Images).WithOne().HasForeignKey(x => x.RecipeId).OnDelete(DeleteBehavior.Cascade);
        recipe.HasMany(x => x.Tags).WithOne().HasForeignKey(x => x.RecipeId).OnDelete(DeleteBehavior.Cascade);
        model.Entity<RecipeTag>().HasKey(x => new { x.RecipeId, x.TagId });
        model.Entity<RecipeTag>().HasOne<Tag>().WithMany().HasForeignKey(x => x.TagId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<IngredientGroup>().HasMany(x => x.Ingredients).WithOne().HasForeignKey(x => x.IngredientGroupId).OnDelete(DeleteBehavior.Cascade);
        model.Entity<IngredientGroup>().HasIndex(x => new { x.RecipeId, x.SortOrder });
        model.Entity<CookingStep>().HasIndex(x => new { x.RecipeId, x.SortOrder });
        var item = model.Entity<RecipeIngredient>();
        item.ToTable("RecipeIngredients", t => t.HasCheckConstraint("CK_Ingredient_Quantity",
            "(\"AmountValue\" IS NULL AND \"AmountMaximumValue\" IS NULL AND \"UnitId\" IS NULL) OR (\"AmountValue\" IS NOT NULL AND \"AmountValue\" > 0 AND \"UnitId\" IS NOT NULL AND (\"AmountMaximumValue\" IS NULL OR \"AmountMaximumValue\" > \"AmountValue\"))"));
        item.HasOne<Ingredient>().WithMany().HasForeignKey(x => x.IngredientId).OnDelete(DeleteBehavior.Restrict);
        item.HasOne<Unit>().WithMany().HasForeignKey(x => x.UnitId).OnDelete(DeleteBehavior.Restrict);
        item.OwnsOne(x => x.Quantity, q =>
        {
            q.Property(x => x.Value).HasColumnName("AmountValue");
            q.Property(x => x.MaximumValue).HasColumnName("AmountMaximumValue");
        });
        item.Navigation(x => x.Quantity).IsRequired();
        item.HasIndex(x => new { x.IngredientGroupId, x.SortOrder });
        model.Entity<RecipeImage>().HasIndex(x => x.RecipeId).IsUnique().HasFilter("\"IsPrimary\" = TRUE");

        model.Entity<Ingredient>().Property(x => x.Status).HasConversion<string>();
        model.Entity<Ingredient>().HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Ingredient>().HasIndex(x => new { x.Status, x.CreatedByUserId });
        model.Entity<Ingredient>().ToTable("Ingredients", t => t.HasCheckConstraint("CK_Ingredient_Status", "\"Status\" IN ('Active', 'PendingReview')"));
        model.Entity<Ingredient>().HasMany(x => x.Names).WithOne().HasForeignKey(x => x.IngredientId);
        model.Entity<Ingredient>().HasMany(x => x.Aliases).WithOne().HasForeignKey(x => x.IngredientId);
        model.Entity<IngredientName>().HasKey(x => new { x.IngredientId, x.Language });
        model.Entity<IngredientName>().Property(x => x.Name).HasMaxLength(200).UseCollation("recipe_text");
        model.Entity<IngredientName>().Property(x => x.Language).HasMaxLength(35);
        model.Entity<IngredientAlias>().Property(x => x.Name).HasMaxLength(200).UseCollation("recipe_text");
        model.Entity<IngredientAlias>().Property(x => x.Language).HasMaxLength(35);
        model.Entity<RecipeType>().HasIndex(x => x.Code).IsUnique();
        model.Entity<RecipeType>().ToTable("RecipeTypes", t => t.HasCheckConstraint("CK_RecipeType_SortOrder", "\"SortOrder\" >= 0"));
        model.Entity<RecipeType>().HasMany(x => x.Names).WithOne().HasForeignKey(x => x.RecipeTypeId);
        model.Entity<RecipeType>().HasMany(x => x.Aliases).WithOne().HasForeignKey(x => x.RecipeTypeId);
        model.Entity<RecipeTypeAlias>().Property(x => x.Name).HasMaxLength(200).UseCollation("recipe_text");
        model.Entity<RecipeTypeAlias>().Property(x => x.Language).HasMaxLength(35);
        model.Entity<RecipeTypeName>().Property(x => x.Name).UseCollation("recipe_text");
        model.Entity<RecipeTypeName>().HasKey(x => new { x.RecipeTypeId, x.Language });
        model.Entity<Tag>().HasIndex(x => x.Code).IsUnique();
        model.Entity<Tag>().HasMany(x => x.Names).WithOne().HasForeignKey(x => x.TagId);
        model.Entity<Tag>().HasMany(x => x.Aliases).WithOne().HasForeignKey(x => x.TagId);
        model.Entity<TagAlias>().Property(x => x.Name).HasMaxLength(200).UseCollation("recipe_text");
        model.Entity<TagAlias>().Property(x => x.Language).HasMaxLength(35);
        model.Entity<TagName>().HasKey(x => new { x.TagId, x.Language });
        model.Entity<TagName>().Property(x => x.Name).UseCollation("recipe_text");
        model.Entity<Unit>().HasIndex(x => x.Code).IsUnique();
        model.Entity<Unit>().Property(x => x.Dimension).HasConversion<string>();
        model.Entity<Unit>().HasMany(x => x.Names).WithOne().HasForeignKey(x => x.UnitId);
        model.Entity<UnitName>().HasKey(x => new { x.UnitId, x.Language });

        // Application-generated GUIDs identify new children before their first save.
        foreach (var entity in model.Model.GetEntityTypes().Where(e => !e.IsOwned()))
        {
            var id = entity.FindProperty("Id");
            if (id?.ClrType == typeof(Guid)) id.ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never;
        }
    }
}
