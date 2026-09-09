using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vanos.API.Data;
using Vanos.API.Models;
using Vanos.API.Services;
namespace Vanos.API.Tests;

public class TrackingIntegrationTests
{
    public sealed class Location
    {
        public int DriverId { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    private static HubConnection Connection(ApiFactory factory, string token) =>
        new HubConnectionBuilder().WithUrl("https://localhost/hubs/tracking", options =>
        {
            options.Transports = HttpTransportType.LongPolling;
            options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
            options.AccessTokenProvider = () => Task.FromResult<string?>(token);
        }).Build();

    [Fact]
    public async Task Tracking_IsolatesRecipients_RechecksAssignment_AndRejectsConsumerPublishing()
    {
        using var factory = new ApiFactory();
        using var http = factory.CreateClient();
        string driverToken, ownerToken, otherToken;
        int studentId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureCreatedAsync();
            var owner = new User { Email = "owner@vanos.test", Role = Roles.Parent };
            var other = new User { Email = "other@vanos.test", Role = Roles.Student };
            var driver = new User { Email = "driver@vanos.test", Role = Roles.Driver,
                Driver = new Driver { StudentCapacity = 10 } };
            db.Users.AddRange(owner, other, driver);
            await db.SaveChangesAsync();
            var student = new Student { ParentId = owner.Id, DriverId = driver.DriverId };
            db.Students.Add(student);
            await db.SaveChangesAsync();
            studentId = student.Id;
            var jwt = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
            driverToken = jwt.GenerateToken(driver);
            ownerToken = jwt.GenerateToken(owner);
            otherToken = jwt.GenerateToken(other);
        }
        await using var ownerConnection = Connection(factory, ownerToken);
        await using var otherConnection = Connection(factory, otherToken);
        await using var driverConnection = Connection(factory, driverToken);
        var received = new TaskCompletionSource<Location>(TaskCreationOptions.RunContinuationsAsynchronously);
        var ownerCount = 0;
        var otherCount = 0;
        ownerConnection.On<Location>("LocationUpdated", location =>
        {
            Interlocked.Increment(ref ownerCount);
            received.TrySetResult(location);
        });
        otherConnection.On<Location>("LocationUpdated", _ => Interlocked.Increment(ref otherCount));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await ownerConnection.StartAsync(timeout.Token);
        await otherConnection.StartAsync(timeout.Token);
        await driverConnection.StartAsync(timeout.Token);
        // Invocation completions also ensure that OnConnectedAsync has finished.
        await Assert.ThrowsAsync<HubException>(() => ownerConnection.InvokeAsync("UpdateLocation", -22.9, -47.0, timeout.Token));
        await Assert.ThrowsAsync<HubException>(() => otherConnection.InvokeAsync("UpdateLocation", -22.9, -47.0, timeout.Token));
        await Assert.ThrowsAsync<HubException>(() => driverConnection.InvokeAsync("UpdateLocation", 91.0, 0.0, timeout.Token));
        await driverConnection.InvokeAsync("UpdateLocation", -22.9, -47.0, timeout.Token);
        var location = await received.Task.WaitAsync(timeout.Token);
        Assert.Equal(-22.9, location.Latitude);
        // A round trip acts as an ordering barrier after the location publication.
        await Assert.ThrowsAsync<HubException>(() => otherConnection.InvokeAsync("NoSuchMethod", timeout.Token));
        Assert.Equal(0, Volatile.Read(ref otherCount));
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Students.SingleAsync(s => s.Id == studentId)).DriverId = null;
            await db.SaveChangesAsync();
        }
        // Reconnect publisher to reset its per-connection pacing without sleeping.
        await driverConnection.StopAsync(timeout.Token);
        await driverConnection.StartAsync(timeout.Token);
        await driverConnection.InvokeAsync("UpdateLocation", -22.8, -47.1, timeout.Token);
        await Assert.ThrowsAsync<HubException>(() => ownerConnection.InvokeAsync("NoSuchMethod", timeout.Token));
        Assert.Equal(1, Volatile.Read(ref ownerCount));
        Assert.Equal(0, Volatile.Read(ref otherCount));
    }
}
