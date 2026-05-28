using DirectoryChangeApp.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace DirectoryChangeApp.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<DirectoryState> DirectoryStates { get; set; }
    public DbSet<FileItemEntity> FileItems { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<DirectoryState>()
            .HasIndex(d => d.DirectoryPath)
            .IsUnique();

        modelBuilder.Entity<FileItemEntity>()
            .HasOne(f => f.DirectoryState)
            .WithMany(d => d.FileItems)
            .HasForeignKey(f => f.DirectoryStateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
