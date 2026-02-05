using Microsoft.EntityFrameworkCore;
using XNDmjApi.Loxone.Models;
using XNDmjApi.QrMulti.Models;

namespace XNDmjApi.Loxone.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<LoxoneAction> LoxoneActions => Set<LoxoneAction>();
        public DbSet<Location> Locations => Set<Location>();
        public DbSet<LocationAction> LocationActions => Set<LocationAction>();
        public DbSet<QrToken> QrTokens => Set<QrToken>();

        // QrMulti (multipropòsit)
        public DbSet<QrManifest> QrManifests => Set<QrManifest>();

        
        public DbSet<QrDevice> QrDevices => Set<QrDevice>();
protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<LoxoneAction>()
                .HasIndex(x => x.Code)
                .IsUnique();

            modelBuilder.Entity<Location>()
                .HasIndex(x => x.Code)
                .IsUnique();

            modelBuilder.Entity<LocationAction>()
                .HasIndex(x => x.LocationId)
                .IsUnique();

            modelBuilder.Entity<LocationAction>()
                .HasOne(x => x.Location)
                .WithMany()
                .HasForeignKey(x => x.LocationId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<LocationAction>()
                .HasOne(x => x.LoxoneAction)
                .WithMany()
                .HasForeignKey(x => x.LoxoneActionId)
                .OnDelete(DeleteBehavior.Cascade);

            // ==========================================================
            // QrMulti · QrManifests (multipropòsit)
            // ==========================================================
            modelBuilder.Entity<QrManifest>()
                .HasKey(x => x.Token);

            modelBuilder.Entity<QrManifest>()
                .Property(x => x.PayloadJson)
                .IsRequired();

            modelBuilder.Entity<QrManifest>()
                .HasIndex(x => x.ItemCode);

            modelBuilder.Entity<QrManifest>()
                .HasIndex(x => x.PanelCode);
        

            modelBuilder.Entity<QrDevice>()
                .HasIndex(x => x.DeviceName)
                .IsUnique();
}
    }
}

