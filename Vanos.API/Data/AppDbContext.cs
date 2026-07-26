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
    }
}