using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Vanos.API.Controllers;
using Vanos.API.Data;
using Vanos.API.DTOs;
using Vanos.API.Models;
namespace Vanos.API.Tests;

public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("VANOS_TEST_SQLSERVER")))
            Skip = "Set VANOS_TEST_SQLSERVER to run SQL Server integration tests.";
    }
}

public class SqlServerIntegrationTests
{
    [SqlServerFact]
    public async Task Migrations_PaymentConcurrency_AndSeatAllocation_WorkOnSqlServer()
    {
        // Always create a unique disposable database; never delete the database supplied by the caller.
        var connection = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("VANOS_TEST_SQLSERVER")!)
            { InitialCatalog = "VanosVerify_" + Guid.NewGuid().ToString("N") };
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection.ConnectionString).Options;
        await using var setup = new AppDbContext(options);
        try
        {
            Assert.False(setup.Database.HasPendingModelChanges());
            await setup.Database.MigrateAsync();
            Assert.Contains("20260908213118_CompleteVanosMvp", await setup.Database.GetAppliedMigrationsAsync());
            setup.Drivers.AddRange(new Driver { StudentCapacity = 10 }, new Driver { StudentCapacity = 1 });
            await setup.SaveChangesAsync();
            var drivers = await setup.Drivers.OrderBy(d => d.Id).ToListAsync();
            var student = new Student { ParentId = 10, DriverId = drivers[0].Id };
            var candidateA = new Student { ParentId = 10 };
            var candidateB = new Student { ParentId = 20 };
            setup.Students.AddRange(student, candidateA, candidateB);
            await setup.SaveChangesAsync();
            var fee = new MonthlyFee { StudentId = student.Id, DriverId = drivers[0].Id,
                Amount = 100, DueDate = new DateTime(2026, 10, 10) };
            var hireA = new HireRequest { StudentId = candidateA.Id, DriverId = drivers[1].Id };
            var hireB = new HireRequest { StudentId = candidateB.Id, DriverId = drivers[1].Id };
            setup.MonthlyFees.Add(fee);
            setup.HireRequests.AddRange(hireA, hireB);
            await setup.SaveChangesAsync();
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                { ["Payments:EnableSimulation"] = "true" }).Build();
            async Task<MonthlyFeeResponse> Pay()
            {
                await using var db = new AppDbContext(options);
                var controller = new MonthlyFeesController(db, new TestEnvironment(), config)
                { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext
                    { User = TestHelpers.BuildParentPrincipal(10) } } };
                return Assert.IsType<MonthlyFeeResponse>(Assert.IsType<OkObjectResult>(
                    (await controller.SimulatePayment(fee.Id)).Result).Value);
            }
            var paid = await Task.WhenAll(Pay(), Pay());
            Assert.All(paid, f => Assert.True(f.IsPaid && f.IsSimulated));
            Assert.NotNull(paid[0].PaymentDate);
            Assert.Equal(paid[0].PaymentDate, paid[1].PaymentDate);
            async Task<bool> Accept(int id)
            {
                await using var db = new AppDbContext(options);
                var controller = new HireRequestsController(db) { ControllerContext = new ControllerContext
                    { HttpContext = new DefaultHttpContext { User = TestHelpers.BuildDriverPrincipal(drivers[1].Id) } } };
                try
                {
                    var result = await controller.Accept(id);
                    Assert.True(result is OkObjectResult or ConflictObjectResult);
                    return result is OkObjectResult;
                }
                catch (SqlException ex) when (ex.Number == 1205) { return false; }
                catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && sql.Number == 1205) { return false; }
            }
            var accepted = await Task.WhenAll(Accept(hireA.Id), Accept(hireB.Id));
            Assert.Equal(1, accepted.Count(result => result));
            Assert.Equal(1, await setup.Students.CountAsync(s => s.DriverId == drivers[1].Id));
            Assert.Equal(1, await setup.HireRequests.CountAsync(h => h.DriverId == drivers[1].Id && h.Status == HireRequestStatus.Accepted));
        }
        finally { await setup.Database.EnsureDeletedAsync(); }
    }
}
