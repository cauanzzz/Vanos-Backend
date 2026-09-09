using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Vanos.API.Controllers;
using Vanos.API.Data;
using Vanos.API.DTOs;
using Vanos.API.Models;
namespace Vanos.API.Tests;

public sealed class TestEnvironment : IWebHostEnvironment
{
    public string EnvironmentName { get; set; } = "Development";
    public string ApplicationName { get; set; } = "Vanos.API";
    public string WebRootPath { get; set; } = "";
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    public string ContentRootPath { get; set; } = "";
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}

public class MvpTests
{
    private static MonthlyFeesController Payments(AppDbContext db, int owner = 10,
        string environment = "Development", bool enabled = true) =>
        new(db, new TestEnvironment { EnvironmentName = environment },
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                { ["Payments:EnableSimulation"] = enabled.ToString() }).Build())
        {
            ControllerContext = new ControllerContext
                { HttpContext = new DefaultHttpContext { User = TestHelpers.BuildParentPrincipal(owner) } }
        };

    private static async Task SeedFees(AppDbContext db)
    {
        db.Drivers.Add(new Driver { Id = 1, StudentCapacity = 10 });
        db.Students.AddRange(new Student { Id = 1, ParentId = 10, DriverId = 1 },
            new Student { Id = 2, ParentId = 20, DriverId = 1 });
        db.MonthlyFees.AddRange(
            new MonthlyFee { Id = 1, StudentId = 1, DriverId = 1, Amount = 100, DueDate = new DateTime(2026, 9, 10) },
            new MonthlyFee { Id = 2, StudentId = 2, DriverId = 1, Amount = 100, DueDate = new DateTime(2026, 9, 10) },
            new MonthlyFee { Id = 3, StudentId = 1, DriverId = 1, Amount = 100, DueDate = new DateTime(2026, 8, 10), IsPaid = true });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task PendingInvoices_AreScopedToOwner()
    {
        using var db = TestHelpers.BuildContext();
        await SeedFees(db);
        var result = await Payments(db).GetMine();
        var page = Assert.IsType<PageResult<MonthlyFeeResponse>>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(1, Assert.Single(page.Items).Id);
    }

    [Fact]
    public async Task Simulation_IsIdempotent_AndCannotPayAnotherOwnersInvoice()
    {
        using var db = TestHelpers.BuildContext();
        await SeedFees(db);
        var controller = Payments(db);
        var first = Assert.IsType<MonthlyFeeResponse>(Assert.IsType<OkObjectResult>((await controller.SimulatePayment(1)).Result).Value);
        var second = Assert.IsType<MonthlyFeeResponse>(Assert.IsType<OkObjectResult>((await controller.SimulatePayment(1)).Result).Value);
        Assert.True(first.IsPaid);
        Assert.True(first.IsSimulated);
        Assert.NotNull(first.PaymentDate);
        Assert.Equal(first.PaymentDate, second.PaymentDate);
        Assert.IsType<NotFoundResult>((await controller.SimulatePayment(2)).Result);
        Assert.False((await db.MonthlyFees.AsNoTracking().SingleAsync(f => f.Id == 2)).IsPaid);
    }

    [Fact]
    public async Task ConcurrentSimulations_KeepOnePaymentTimestamp()
    {
        var connectionString = $"Data Source={Guid.NewGuid():N};Mode=Memory;Cache=Shared;Default Timeout=10";
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connectionString).Options;
        await using var keeper = new AppDbContext(options);
        await keeper.Database.OpenConnectionAsync();
        await keeper.Database.EnsureCreatedAsync();
        await SeedFees(keeper);
        async Task<MonthlyFeeResponse> Pay()
        {
            await using var db = new AppDbContext(options);
            return Assert.IsType<MonthlyFeeResponse>(Assert.IsType<OkObjectResult>(
                (await Payments(db).SimulatePayment(1)).Result).Value);
        }
        var results = await Task.WhenAll(Task.Run(Pay), Task.Run(Pay));
        Assert.All(results, f => Assert.True(f.IsPaid && f.IsSimulated));
        Assert.Equal(results[0].PaymentDate, results[1].PaymentDate);
    }

    [Theory]
    [InlineData("Production", true)]
    [InlineData("Staging", true)]
    [InlineData("Development", false)]
    public async Task Simulation_RequiresDevelopmentAndExplicitFlag(string environment, bool enabled)
    {
        using var db = TestHelpers.BuildContext();
        await SeedFees(db);
        var result = await Payments(db, environment: environment, enabled: enabled).SimulatePayment(1);
        Assert.IsType<NotFoundResult>(result.Result);
        Assert.False((await db.MonthlyFees.AsNoTracking().SingleAsync(f => f.Id == 1)).IsPaid);
    }

    [Fact]
    public async Task StudentRole_CanReadOnlyOwnFees()
    {
        using var db = TestHelpers.BuildContext();
        await SeedFees(db);
        var controller = Payments(db);
        controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[] {
            new Claim(ClaimTypes.NameIdentifier, "10"), new Claim(ClaimTypes.Role, Roles.Student) }, "test"));
        var page = Assert.IsType<PageResult<MonthlyFeeResponse>>(Assert.IsType<OkObjectResult>((await controller.GetMine()).Result).Value);
        Assert.Equal(1, Assert.Single(page.Items).StudentId);
    }

    [Fact]
    public async Task Marketplace_FiltersAreasSchoolActiveAndCapacity_AndPaginates()
    {
        using var db = TestHelpers.BuildContext();
        db.Drivers.AddRange(Enumerable.Range(1, 4).Select(id => new Driver
            { Id = id, Fullname = $"Driver {id}", StudentCapacity = 1, IsActive = id != 4 }));
        db.DriverSchools.AddRange(Enumerable.Range(1, 4).Select(id => new DriverSchool { DriverId = id, SchoolId = 5 }));
        db.DriverServiceAreas.AddRange(Enumerable.Range(1, 4).Select(id => new DriverServiceArea
            { DriverId = id, City = "CAMPINAS", Neighborhood = "CENTRO" }));
        db.Students.Add(new Student { Id = 1, ParentId = 10, DriverId = 3 });
        await db.SaveChangesAsync();
        var controller = new DriversController(db);
        var result = await controller.Marketplace(schoolId: 5, city: "Campinas", neighborhood: " Centro ", pageSize: 1);
        var page = Assert.IsType<PageResult<DriverListing>>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(1, Assert.Single(page.Items).Id);
        Assert.True(page.HasNextPage);
        var next = Assert.IsType<PageResult<DriverListing>>(Assert.IsType<OkObjectResult>(
            (await controller.Marketplace(schoolId: 5, city: "Campinas", page: 2, pageSize: 1)).Result).Value);
        Assert.Equal(2, Assert.Single(next.Items).Id);
        Assert.False(next.HasNextPage);
        Assert.IsType<BadRequestObjectResult>((await controller.Marketplace(page: 0)).Result);
    }

    [Fact]
    public async Task Acceptance_RejectsFullVan_WithoutAssigningStudent()
    {
        using var db = TestHelpers.BuildContext();
        db.Drivers.Add(new Driver { Id = 1, StudentCapacity = 1 });
        db.Students.AddRange(new Student { Id = 1, DriverId = 1 }, new Student { Id = 2 });
        db.HireRequests.Add(new HireRequest { Id = 1, DriverId = 1, StudentId = 2 });
        await db.SaveChangesAsync();
        var controller = new HireRequestsController(db) { ControllerContext = new ControllerContext
            { HttpContext = new DefaultHttpContext { User = TestHelpers.BuildDriverPrincipal(1) } } };
        Assert.IsType<ConflictObjectResult>(await controller.Accept(1));
        Assert.Null((await db.Students.FindAsync(2))!.DriverId);
        Assert.Equal(HireRequestStatus.Pending, (await db.HireRequests.FindAsync(1))!.Status);
    }
}
