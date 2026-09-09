using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Vanos.API.Data;
using Vanos.API.Models;
using Vanos.API.Services;
namespace Vanos.API.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection connection = new($"Data Source=Vanos_{Guid.NewGuid():N};Mode=Memory;Cache=Shared;Pooling=False;Default Timeout=10");
    public ApiFactory() => connection.Open();
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.RemoveAll<AppDbContext>();
            services.AddDbContext<AppDbContext>(o => o.UseSqlite(connection.ConnectionString));
        });
    }
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) connection.Dispose();
    }
}

public class HttpAuthorizationTests
{
    [Fact]
    public async Task Middleware_EnforcesAuthenticationRolesAndQueryTokenScope()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        string token;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureCreatedAsync();
            var account = new User { Email = "student@example.com", Role = Roles.Student };
            db.Users.Add(account);
            await db.SaveChangesAsync();
            token = scope.ServiceProvider.GetRequiredService<IJwtTokenService>().GenerateToken(account);
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/students")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/students?access_token=" + token)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/hubs/tracking/negotiate?negotiateVersion=1", null)).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/students")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/drivers")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/drivers/marketplace")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/monthlyfees/mine")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/hubs/tracking/negotiate?negotiateVersion=1", null)).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Users.SingleAsync()).Role = Roles.Parent;
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/students")).StatusCode);
    }
}
