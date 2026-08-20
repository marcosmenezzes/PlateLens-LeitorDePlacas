using Microsoft.EntityFrameworkCore;
using PlateLens.Domain.Entities;

namespace PlateLens.Infra.Data;

/// <summary>Mapeia as entidades do domínio para o banco SQLite e preenche metadados comuns.</summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public static readonly Guid NativeCameraId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public DbSet<Camera> Cameras => Set<Camera>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<AccessEvent> AccessEvents => Set<AccessEvent>();
    public DbSet<RecognitionAttempt> RecognitionAttempts => Set<RecognitionAttempt>();

    /// <summary>Define índices, limites de campos, relacionamentos e a câmera nativa inicial.</summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Camera>().HasIndex(x => x.IpAddress).IsUnique();
        modelBuilder.Entity<Camera>().Property(x => x.Name).HasMaxLength(64);
        modelBuilder.Entity<Vehicle>().HasIndex(x => x.Plate).IsUnique();
        modelBuilder.Entity<Vehicle>().Property(x => x.Plate).HasMaxLength(7);
        modelBuilder.Entity<Vehicle>().Property(x => x.Name).HasMaxLength(80);
        modelBuilder.Entity<AccessEvent>().Property(x => x.PlateDetected).HasMaxLength(7);
        modelBuilder.Entity<RecognitionAttempt>().HasIndex(x => x.OccurredAt);
        modelBuilder.Entity<RecognitionAttempt>().Property(x => x.RawText).HasMaxLength(64);
        modelBuilder.Entity<RecognitionAttempt>().Property(x => x.NormalizedPlate).HasMaxLength(7);
        modelBuilder.Entity<RecognitionAttempt>().Property(x => x.PlateType).HasMaxLength(16);
        modelBuilder.Entity<RecognitionAttempt>().Property(x => x.RejectionReason).HasMaxLength(32);
        modelBuilder.Entity<Camera>().HasData(new Camera
        {
            Id = NativeCameraId, Name = "Câmera nativa", SourceKind = CameraSourceKind.Native,
            DeviceIndex = 0, IsActive = true, RegionX = .2, RegionY = .25,
            RegionWidth = .6, RegionHeight = .5, CreatedAt = new DateTime(2026, 8, 20, 0, 0, 0, DateTimeKind.Utc)
        });
    }

    /// <summary>Gera identificadores e datas de criação/alteração antes de persistir uma unidade de trabalho.</summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Entity.Id == Guid.Empty) entry.Entity.Id = Guid.NewGuid();
                if (entry.Entity.CreatedAt == default) entry.Entity.CreatedAt = now;
            }
            if (entry.State == EntityState.Modified) entry.Entity.UpdatedAt = now;
        }
        return await base.SaveChangesAsync(cancellationToken);
    }
}
