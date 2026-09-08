using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vanos.API.DTOs;
using Vanos.API.Extensions;
using Vanos.API.Models;
namespace Vanos.API.Controllers;

public partial class DriversController
{
    [HttpPut("{id:int}/service-areas")]
    [Authorize(Roles = Roles.Driver)]
    public async Task<IActionResult> UpdateServiceAreas(int id,
        [FromBody] List<ServiceAreaRequest> areas, CancellationToken ct = default)
    {
        if (id != User.GetDriverId()) return Forbid();
        if (!await _context.Drivers.AnyAsync(d => d.Id == id, ct)) return NotFound();
        if (areas is null || areas.Count > 100 || areas.Any(a => a is null ||
            string.IsNullOrWhiteSpace(a.City) || string.IsNullOrWhiteSpace(a.Neighborhood) ||
            a.City.Length > 100 || a.Neighborhood.Length > 100))
            return BadRequest("Informe até 100 áreas com cidade e bairro (até 100 caracteres cada).");
        var desired = areas.Select(a => (City: a.City.Trim().ToUpperInvariant(),
            Neighborhood: a.Neighborhood.Trim().ToUpperInvariant())).ToHashSet();
        var existing = await _context.DriverServiceAreas.Where(a => a.DriverId == id).ToListAsync(ct);
        var current = existing.Select(a => (a.City, a.Neighborhood)).ToHashSet();
        _context.DriverServiceAreas.RemoveRange(existing.Where(a => !desired.Contains((a.City, a.Neighborhood))));
        foreach (var item in desired.Except(current))
            _context.DriverServiceAreas.Add(new DriverServiceArea
                { DriverId = id, City = item.City, Neighborhood = item.Neighborhood });
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("marketplace")]
    [Authorize(Roles = Roles.Consumer)]
    public async Task<ActionResult<PageResult<DriverListing>>> Marketplace(
        [FromQuery] int? schoolId = null, [FromQuery] string? city = null,
        [FromQuery] string? neighborhood = null, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        if (schoolId is <= 0 || page is < 1 or > 100000 || pageSize is < 1 or > 100 ||
            city?.Length > 100 || neighborhood?.Length > 100)
            return BadRequest("Filtros ou paginação inválidos.");
        city = city?.Trim().ToUpperInvariant();
        neighborhood = neighborhood?.Trim().ToUpperInvariant();
        var query = _context.Drivers.AsNoTracking().Where(d => d.IsActive);
        if (schoolId.HasValue)
            query = query.Where(d => _context.DriverSchools.Any(ds =>
                ds.DriverId == d.Id && ds.SchoolId == schoolId.Value));
        if (!string.IsNullOrEmpty(city) || !string.IsNullOrEmpty(neighborhood))
            query = query.Where(d => _context.DriverServiceAreas.Any(a => a.DriverId == d.Id &&
                (string.IsNullOrEmpty(city) || a.City == city) &&
                (string.IsNullOrEmpty(neighborhood) || a.Neighborhood == neighborhood)));
        // Current assignments, not historical accepted requests, define occupied seats.
        var listings = query.Select(d => new
        {
            d.Id, d.Fullname, d.StudentCapacity,
            AvailableCapacity = d.StudentCapacity - _context.Students.Count(s => s.DriverId == d.Id),
            AverageRating = _context.Ratings.Where(r => r.DriverId == d.Id)
                .Select(r => (double?)r.Score).Average()
        }).Where(d => d.AvailableCapacity > 0);
        var rows = await listings.OrderBy(d => d.Id).Skip((page - 1) * pageSize)
            .Take(pageSize + 1).Select(d => new DriverListing(d.Id, d.Fullname,
                d.StudentCapacity, d.AvailableCapacity, d.AverageRating)).ToListAsync(ct);
        return Ok(new PageResult<DriverListing>(page, pageSize, rows.Count > pageSize,
            rows.Take(pageSize).ToArray()));
    }
}
