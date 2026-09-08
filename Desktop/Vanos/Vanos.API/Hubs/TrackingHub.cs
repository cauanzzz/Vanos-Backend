using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Vanos.API.Data;
using Vanos.API.Extensions;
using Vanos.API.Models;
namespace Vanos.API.Hubs;

[Authorize(Roles = Roles.Driver + "," + Roles.Consumer)]
public sealed class TrackingHub(AppDbContext db) : Hub
{
    private static string Group(int userId) => $"tracking:consumer:{userId}";

    public override async Task OnConnectedAsync()
    {
        var id = Context.User!.GetUserId();
        var account = await db.Users.AsNoTracking().SingleOrDefaultAsync(
            u => u.Id == id, Context.ConnectionAborted);
        if (account is null || !Context.User.IsInRole(account.Role))
        {
            Context.Abort();
            return;
        }
        if (account.Role == Roles.Parent || account.Role == Roles.Student)
            await Groups.AddToGroupAsync(Context.ConnectionId, Group(id), Context.ConnectionAborted);
        await base.OnConnectedAsync();
    }

    [Authorize(Roles = Roles.Driver)]
    public async Task UpdateLocation(double latitude, double longitude)
    {
        if (!double.IsFinite(latitude) || !double.IsFinite(longitude) ||
            latitude is < -90 or > 90 || longitude is < -180 or > 180)
            throw new HubException("Coordenadas inválidas.");
        var userId = Context.User!.GetUserId();
        var driverId = Context.User.GetDriverId();
        var ct = Context.ConnectionAborted;
        if (!await db.Users.AnyAsync(u => u.Id == userId && u.Role == Roles.Driver && u.DriverId == driverId, ct) ||
            !await db.Drivers.AnyAsync(d => d.Id == driverId && d.IsActive, ct))
            throw new HubException("Motorista sem autorização.");

        // Per-connection pacing; production should also limit per driver across connections.
        var now = DateTime.UtcNow;
        if (Context.Items.TryGetValue("lastLocation", out var previous) &&
            previous is DateTime last && now - last < TimeSpan.FromSeconds(1))
            throw new HubException("Aguarde um segundo entre atualizações.");
        Context.Items["lastLocation"] = now;

        // Current ownership is checked on every message: stale subscriptions receive no later updates.
        var recipients = await (from s in db.Students
            join u in db.Users on s.ParentId equals u.Id
            where s.DriverId == driverId && (u.Role == Roles.Parent || u.Role == Roles.Student)
            select u.Id).Distinct().ToListAsync(ct);
        if (recipients.Count == 0) return;
        await Clients.Groups(recipients.Select(Group).ToArray()).SendAsync("LocationUpdated",
            new { DriverId = driverId, Latitude = latitude, Longitude = longitude, RecordedAtUtc = now }, ct);
    }
}
