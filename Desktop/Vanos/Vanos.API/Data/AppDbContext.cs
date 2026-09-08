using Microsoft.EntityFrameworkCore;
using Vanos.API.Models;

namespace Vanos.API.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Driver> Drivers { get; set; }
        public DbSet<Student> Students { get; set; }
        public DbSet<School> Schools { get; set; }
        public DbSet<MonthlyFee> MonthlyFees { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<DriverSchool> DriverSchools { get; set; }
        public DbSet<HireRequest> HireRequests { get; set; }
        public DbSet<Rating> Ratings { get; set; }

        public DbSet<DriverServiceArea> DriverServiceAreas => Set<DriverServiceArea>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<User>().Property(u => u.Email).HasMaxLength(254);
            modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();
            modelBuilder.Entity<User>().HasOne(u => u.Driver).WithMany()
                .HasForeignKey(u => u.DriverId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<DriverServiceArea>(e =>
            {
                e.HasKey(a => new { a.DriverId, a.City, a.Neighborhood });
                e.Property(a => a.City).HasMaxLength(100);
                e.Property(a => a.Neighborhood).HasMaxLength(100);
                e.HasIndex(a => new { a.City, a.Neighborhood });
                e.HasOne<Driver>().WithMany().HasForeignKey(a => a.DriverId).OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<Student>().HasIndex(s => s.DriverId);
            modelBuilder.Entity<Student>().HasIndex(s => new { s.ParentId, s.DriverId });
            modelBuilder.Entity<DriverSchool>().HasIndex(s => new { s.SchoolId, s.DriverId });
            modelBuilder.Entity<MonthlyFee>(e =>
            {
                e.Property(f => f.Amount).HasPrecision(18, 2);
                e.HasIndex(f => new { f.StudentId, f.IsPaid, f.DueDate });
                e.HasIndex(f => new { f.StudentId, f.DriverId, f.DueDate }).IsUnique();
                e.HasOne<Student>().WithMany().HasForeignKey(f => f.StudentId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne<Driver>().WithMany().HasForeignKey(f => f.DriverId).OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<DriverSchool>().HasKey(ds => new { ds.DriverId, ds.SchoolId });
            modelBuilder.Entity<Rating>().HasIndex(r => new { r.DriverId, r.ParentId }).IsUnique();
        }
    }
}