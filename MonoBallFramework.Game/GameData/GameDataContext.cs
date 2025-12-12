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

    // NPC-related entities
    public DbSet<NpcDefinition> Npcs { get; set; } = null!;
    public DbSet<TrainerDefinition> Trainers { get; set; } = null!;

    // Map entities
    public DbSet<MapDefinition> Maps { get; set; } = null!;

    // Audio entities
    public DbSet<AudioDefinition> AudioDefinitions { get; set; } = null!;

    // Popup entities
    public DbSet<PopupTheme> PopupThemes { get; set; } = null!;
    public DbSet<MapSection> MapSections { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureNpcDefinition(modelBuilder);
        ConfigureTrainerDefinition(modelBuilder);
        ConfigureMapDefinition(modelBuilder);
        ConfigureAudioDefinition(modelBuilder);
        ConfigurePopupTheme(modelBuilder);
        ConfigureMapSection(modelBuilder);
    }

    /// <summary>
    ///     Configure NpcDefinition entity.
    /// </summary>
    private void ConfigureNpcDefinition(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<NpcDefinition>(entity =>
        {
            entity.HasKey(n => n.NpcId);

            // Indexes for common queries
            entity.HasIndex(n => n.NpcType);
            entity.HasIndex(n => n.DisplayName);

            // Value converter for GameNpcId
            entity.Property(n => n.NpcId).HasConversion(new GameNpcIdValueConverter());

            // Value converter for GameSpriteId
            entity.Property(n => n.SpriteId).HasConversion(new GameSpriteIdValueConverter());
        });
    }

    /// <summary>
    ///     Configure TrainerDefinition entity.
    /// </summary>
    private void ConfigureTrainerDefinition(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TrainerDefinition>(entity =>
        {
            entity.HasKey(t => t.TrainerId);

            // Indexes for common queries
            entity.HasIndex(t => t.TrainerClass);
            entity.HasIndex(t => t.DisplayName);

            // Value converter for GameTrainerId
            entity.Property(t => t.TrainerId).HasConversion(new GameTrainerIdValueConverter());

            // Value converter for GameSpriteId
            entity.Property(t => t.SpriteId).HasConversion(new GameSpriteIdValueConverter());

            // PartyJson will be deserialized on-demand
        });
    }

    /// <summary>
    ///     Configure MapDefinition entity.
    /// </summary>
    private void ConfigureMapDefinition(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MapDefinition>(entity =>
        {
            entity.HasKey(m => m.MapId);

            // Indexes for common queries
            entity.HasIndex(m => m.Region);
            entity.HasIndex(m => m.MapType);
            entity.HasIndex(m => m.DisplayName);

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
    ///     Configure PopupTheme entity.
    /// </summary>
    private void ConfigurePopupTheme(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PopupTheme>(entity =>
        {
            entity.HasKey(t => t.Id);

            // Value converter for GameThemeId
            entity.Property(t => t.Id).HasConversion(new GameThemeIdValueConverter());

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
    ///     Configure AudioDefinition entity.
    /// </summary>
    private void ConfigureAudioDefinition(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AudioDefinition>(entity =>
        {
            entity.HasKey(a => a.AudioId);

            // Value converter for GameAudioId
            entity.Property(a => a.AudioId).HasConversion(new GameAudioIdValueConverter());

            // Indexes for common queries
            entity.HasIndex(a => a.Category);
            entity.HasIndex(a => a.Subcategory);
            entity.HasIndex(a => a.DisplayName);

            // Composite index for category + subcategory lookups
            entity.HasIndex(a => new { a.Category, a.Subcategory });
        });
    }

    /// <summary>
    ///     Configure MapSection entity.
    /// </summary>
    private void ConfigureMapSection(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MapSection>(entity =>
        {
            entity.HasKey(s => s.Id);

            // Value converters for unified ID types
            entity.Property(s => s.Id).HasConversion(new GameMapSectionIdValueConverter());
            entity.Property(s => s.ThemeId).HasConversion(new GameThemeIdValueConverter());

            // Indexes for common queries
            entity.HasIndex(s => s.Name);
            entity.HasIndex(s => s.ThemeId);

            // Composite index for region map coordinates
            entity.HasIndex(s => new { s.X, s.Y });
        });
    }
}
