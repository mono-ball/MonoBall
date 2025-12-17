using Microsoft.EntityFrameworkCore;
using MonoBallFramework.Game.Engine.Core.Types;
using MonoBallFramework.Game.GameData.Entities;
using MonoBallFramework.Game.GameData.ValueConverters;

namespace MonoBallFramework.Game.GameData;

/// <summary>
///     Factory for creating GameDataContext instances on-demand.
///     Used by singleton services (like AudioRegistry) that need database access
///     without holding a context reference for their entire lifetime.
/// </summary>
public class GameDataContextFactory : IDbContextFactory<GameDataContext>
{
    private readonly DbContextOptions<GameDataContext> _options;

    public GameDataContextFactory(DbContextOptions<GameDataContext> options)
    {
        _options = options;
    }

    public GameDataContext CreateDbContext()
    {
        return new GameDataContext(_options);
    }
}

/// <summary>
///     EF Core DbContext for game data definitions (NPCs, trainers, maps, etc.).
///     Uses in-memory database for fast, read-only access.
/// </summary>
public class GameDataContext : DbContext
{
    // TODO: Add when implementing Pokemon system
    // public DbSet<Species> Species { get; set; } = null!;
    // public DbSet<Move> Moves { get; set; } = null!;
    // public DbSet<Item> Items { get; set; } = null!;
    // public DbSet<Ability> Abilities { get; set; } = null!;

    public GameDataContext(DbContextOptions<GameDataContext> options)
        : base(options) { }

    // Map entities
    public DbSet<MapEntity> Maps { get; set; } = null!;

    // Audio entities
    public DbSet<AudioEntity> Audios { get; set; } = null!;

    // Popup entities
    public DbSet<PopupThemeEntity> PopupThemes { get; set; } = null!;
    public DbSet<MapSectionEntity> MapSections { get; set; } = null!;

    // === NEW: Unified Definition DbSets ===

    // Sprite entities
    public DbSet<SpriteEntity> Sprites { get; set; } = null!;

    // Popup background and outline entities
    public DbSet<PopupBackgroundEntity> PopupBackgrounds { get; set; } = null!;
    public DbSet<PopupOutlineEntity> PopupOutlines { get; set; } = null!;

    // Behavior entities
    public DbSet<BehaviorEntity> Behaviors { get; set; } = null!;
    public DbSet<TileBehaviorEntity> TileBehaviors { get; set; } = null!;

    // Font entities
    public DbSet<FontEntity> Fonts { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureMapEntity(modelBuilder);
        ConfigureAudioEntity(modelBuilder);
        ConfigurePopupTheme(modelBuilder);
        ConfigureMapSection(modelBuilder);

        // NEW: Configure unified definition entities
        ConfigureSpriteDefinition(modelBuilder);
        ConfigurePopupBackground(modelBuilder);
        ConfigurePopupOutline(modelBuilder);
        ConfigureBehaviorDefinition(modelBuilder);
        ConfigureTileBehaviorDefinition(modelBuilder);
        ConfigureFontDefinition(modelBuilder);
    }

    /// <summary>
    ///     Configure MapEntity entity.
    /// </summary>
    private void ConfigureMapEntity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MapEntity>(entity =>
        {
            entity.HasKey(m => m.MapId);

            // Indexes for common queries
            entity.HasIndex(m => m.Region);
            entity.HasIndex(m => m.MapType);
            entity.HasIndex(m => m.Name);

            // Value converters for GameMapId (unified format)
            var mapIdConverter = new GameMapIdValueConverter();
            var nullableMapIdConverter = new NullableGameMapIdValueConverter();
            entity.Property(m => m.MapId).HasConversion(mapIdConverter);
            entity.Property(m => m.NorthMapId).HasConversion(nullableMapIdConverter);
            entity.Property(m => m.SouthMapId).HasConversion(nullableMapIdConverter);
            entity.Property(m => m.EastMapId).HasConversion(nullableMapIdConverter);
            entity.Property(m => m.WestMapId).HasConversion(nullableMapIdConverter);

            // Value converter for RegionMapSection (GameMapSectionId)
            entity.Property(m => m.RegionMapSection).HasConversion(new NullableGameMapSectionIdValueConverter());

            // Value converter for MusicId (GameAudioId)
            entity.Property(m => m.MusicId).HasConversion(new NullableGameAudioIdValueConverter());

            // TiledDataJson stores complete Tiled map data
            // Will be deserialized on-demand by MapLoader
        });
    }

    /// <summary>
    ///     Configure PopupThemeEntity entity.
    /// </summary>
    private void ConfigurePopupTheme(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PopupThemeEntity>(entity =>
        {
            entity.HasKey(t => t.ThemeId);

            // Value converter for GameThemeId
            entity.Property(t => t.ThemeId).HasConversion(new GameThemeIdValueConverter());

            // Indexes for common queries
            entity.HasIndex(t => t.Name);
            entity.HasIndex(t => t.Background);
            entity.HasIndex(t => t.Outline);

            // Configure relationship with MapSections
            entity
                .HasMany(t => t.MapSections)
                .WithOne(s => s.Theme)
                .HasForeignKey(s => s.ThemeId)
                .OnDelete(DeleteBehavior.Restrict); // Prevent deleting themes with sections
        });
    }

    /// <summary>
    ///     Configure AudioEntity entity.
    /// </summary>
    private void ConfigureAudioEntity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AudioEntity>(entity =>
        {
            entity.HasKey(a => a.AudioId);

            // Value converter for GameAudioId
            entity.Property(a => a.AudioId).HasConversion(new GameAudioIdValueConverter());

            // Indexes for common queries
            entity.HasIndex(a => a.Category);
            entity.HasIndex(a => a.Subcategory);
            entity.HasIndex(a => a.Name);

            // Composite index for category + subcategory lookups
            entity.HasIndex(a => new { a.Category, a.Subcategory });
        });
    }

    /// <summary>
    ///     Configure MapSectionEntity entity.
    /// </summary>
    private void ConfigureMapSection(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MapSectionEntity>(entity =>
        {
            entity.HasKey(s => s.MapSectionId);

            // Value converters for unified ID types
            entity.Property(s => s.MapSectionId).HasConversion(new GameMapSectionIdValueConverter());
            entity.Property(s => s.ThemeId).HasConversion(new GameThemeIdValueConverter());

            // Indexes for common queries
            entity.HasIndex(s => s.Name);
            entity.HasIndex(s => s.ThemeId);

            // Composite index for region map coordinates
            entity.HasIndex(s => new { s.X, s.Y });
        });
    }

    // ============================================================================
    // NEW: Unified Definition Entity Configurations
    // ============================================================================

    /// <summary>
    ///     Configure SpriteEntity with owned entity collections.
    ///     In-memory provider stores nested collections directly without JSON serialization.
    /// </summary>
    private void ConfigureSpriteDefinition(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SpriteEntity>(entity =>
        {
            entity.HasKey(s => s.SpriteId);

            // Value converter for GameSpriteId
            entity.Property(s => s.SpriteId).HasConversion(new GameSpriteIdNonNullableValueConverter());

            // Indexes for common queries
            entity.HasIndex(s => s.Name);
            entity.HasIndex(s => s.Type);
            entity.HasIndex(s => s.TexturePath);

            // Configure Frames as owned collection
            // In-memory provider stores these directly without JSON serialization
            entity.OwnsMany(s => s.Frames, framesBuilder =>
            {
                framesBuilder.WithOwner().HasForeignKey("SpriteEntitySpriteId");
                framesBuilder.Property<int>("Id").ValueGeneratedOnAdd();
                framesBuilder.HasKey("Id");
            });

            // Configure Animations as owned collection
            // EF Core 8+ natively supports primitive collections (List<int>, List<double>)
            entity.OwnsMany(s => s.Animations, animBuilder =>
            {
                animBuilder.WithOwner().HasForeignKey("SpriteEntitySpriteId");
                animBuilder.Property<int>("Id").ValueGeneratedOnAdd();
                animBuilder.HasKey("Id");
            });
        });
    }

    /// <summary>
    ///     Configure PopupBackgroundEntity.
    /// </summary>
    private void ConfigurePopupBackground(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PopupBackgroundEntity>(entity =>
        {
            entity.HasKey(b => b.BackgroundId);

            // Value converter for GamePopupBackgroundId
            entity.Property(b => b.BackgroundId).HasConversion(new GamePopupBackgroundIdValueConverter());

            // Indexes for common queries
            entity.HasIndex(b => b.Name);
            entity.HasIndex(b => b.Type);
        });
    }

    /// <summary>
    ///     Configure PopupOutlineEntity with owned entity collections.
    ///     In-memory provider stores nested collections directly without JSON serialization.
    /// </summary>
    private void ConfigurePopupOutline(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PopupOutlineEntity>(entity =>
        {
            entity.HasKey(o => o.OutlineId);

            // Value converter for GamePopupOutlineId
            entity.Property(o => o.OutlineId).HasConversion(new GamePopupOutlineIdValueConverter());

            // Indexes for common queries
            entity.HasIndex(o => o.Name);
            entity.HasIndex(o => o.Type);

            // Configure Tiles as owned collection
            entity.OwnsMany(o => o.Tiles, tilesBuilder =>
            {
                tilesBuilder.WithOwner().HasForeignKey("PopupOutlineEntityOutlineId");
                tilesBuilder.Property<int>("Id").ValueGeneratedOnAdd();
                tilesBuilder.HasKey("Id");
            });

            // Configure TileUsage as owned single object
            entity.OwnsOne(o => o.TileUsage);
        });
    }

    /// <summary>
    ///     Configure BehaviorEntity.
    /// </summary>
    private void ConfigureBehaviorDefinition(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BehaviorEntity>(entity =>
        {
            entity.HasKey(b => b.BehaviorId);

            // Value converter for GameBehaviorId
            entity.Property(b => b.BehaviorId).HasConversion(new GameBehaviorIdValueConverter());

            // Indexes for common queries
            entity.HasIndex(b => b.Name);
        });
    }

    /// <summary>
    ///     Configure TileBehaviorEntity.
    /// </summary>
    private void ConfigureTileBehaviorDefinition(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TileBehaviorEntity>(entity =>
        {
            entity.HasKey(t => t.TileBehaviorId);

            // Value converter for GameTileBehaviorId
            entity.Property(t => t.TileBehaviorId).HasConversion(new GameTileBehaviorIdValueConverter());

            // Indexes for common queries
            entity.HasIndex(t => t.Name);
            entity.HasIndex(t => t.Flags);
        });
    }

    /// <summary>
    ///     Configure FontEntity.
    /// </summary>
    private void ConfigureFontDefinition(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FontEntity>(entity =>
        {
            entity.HasKey(f => f.FontId);

            // Value converter for GameFontId
            entity.Property(f => f.FontId).HasConversion(new GameFontIdValueConverter());

            // Indexes for common queries
            entity.HasIndex(f => f.Name);
            entity.HasIndex(f => f.Category);
            entity.HasIndex(f => f.FontPath);
        });
    }
}
