using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Vanos.API.Data;
using Vanos.API.Models;

namespace Vanos.API.Tests
{
    public static class TestHelpers
    {
        public static AppDbContext BuildContext()
        {
            var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
            connection.Open();
            var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection, contextOwnsConnection: true).Options);
            context.Database.EnsureCreated();
            return context;
        }

        public static ClaimsPrincipal BuildParentPrincipal(int userId) =>
            new(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, Roles.Parent)
            }, "TestAuth"));

        public static ClaimsPrincipal BuildDriverPrincipal(int driverId, int userId = 0) =>
            new(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, Roles.Driver),
                new Claim("driverId", driverId.ToString())
            }, "TestAuth"));
    }
}
