using Microsoft.EntityFrameworkCore;
using HoNfigurator.ManagementPortal.Models;

namespace HoNfigurator.ManagementPortal.Data;

public class PortalDbContext : DbContext
{
    public PortalDbContext(DbContextOptions<PortalDbContext> options) : base(options)
    {
    }
    
    public DbSet<PortalUser> Users { get; set; } = null!;
    public DbSet<RegisteredServer> Servers { get; set; } = null!;
    public DbSet<ServerAccess> ServerAccess { get; set; } = null!;
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // User configuration
        modelBuilder.Entity<PortalUser>(entity =>
        {
            entity.HasIndex(e => e.DiscordId).IsUnique();
            entity.HasIndex(e => e.SessionToken);
            
            entity.HasMany(e => e.Servers)
                  .WithOne(e => e.Owner)
                  .HasForeignKey(e => e.OwnerId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
        
        // Server configuration
        modelBuilder.Entity<RegisteredServer>(entity =>
        {
            entity.HasIndex(e => e.ServerId).IsUnique();
            entity.HasIndex(e => e.ApiKey);
            entity.HasIndex(e => e.OwnerId);
            
            entity.HasMany(e => e.SharedAccess)
                  .WithOne(e => e.Server)
                  .HasForeignKey(e => e.ServerId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
        
        // ServerAccess configuration
        modelBuilder.Entity<ServerAccess>(entity =>
        {
            entity.HasIndex(e => new { e.ServerId, e.DiscordId }).IsUnique();
            entity.HasIndex(e => e.DiscordId);
            entity.HasIndex(e => e.UserId);
        });
    }
}
