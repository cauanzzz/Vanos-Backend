using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vanos.API.Data;
using Vanos.API.Extensions;
using Vanos.API.Models;

namespace Vanos.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public partial class DriversController : ControllerBase
    {
        private readonly AppDbContext _context;

        public DriversController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        [Authorize(Roles = Roles.Driver)]
        public async Task<ActionResult<IEnumerable<Driver>>> GetDrivers()
        {
            var id = User.GetDriverId();
            return await _context.Drivers.AsNoTracking().Where(d => d.Id == id).ToListAsync();
        }

        [NonAction]
        public async Task<ActionResult<Driver>> PostDriver(Driver driver)
        {
            _context.Drivers.Add(driver);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetDrivers), new { id = driver.Id }, driver);
        }

        [HttpGet("{id}/students")]
        [Authorize(Roles = Roles.Driver)]
        public async Task<ActionResult<IEnumerable<Student>>> GetStudentsByDriver(int id)
        {
            if (id != User.GetDriverId()) return Forbid();
            var students = await _context.Students
                                         .Where(s => s.DriverId == id)
                                         .ToListAsync();

            if (!students.Any())
            {
                return NotFound("Nenhum aluno encontrado para a van deste motorista.");
            }

            return students;
        }

        [HttpPut("{id}/schools")]
        [Authorize(Roles = Roles.Driver)]
        public async Task<IActionResult> UpdateSchoolsServed(int id, [FromBody] List<int> schoolIds)
        {
            if (id != User.GetDriverId())
            {
                return Forbid();
            }

            var driverExists = await _context.Drivers.AnyAsync(d => d.Id == id);
            if (!driverExists)
            {
                return NotFound("Motorista não encontrado.");
            }

            if (schoolIds is null || schoolIds.Count > 100 || schoolIds.Any(id => id <= 0))
                return BadRequest("Informe até 100 instituições válidas.");
            var desired = schoolIds.Distinct().ToHashSet();
            if (await _context.Schools.CountAsync(s => desired.Contains(s.Id)) != desired.Count)
                return BadRequest("Instituição não encontrada.");
            var existing = await _context.DriverSchools.Where(ds => ds.DriverId == id).ToListAsync();
            var current = existing.Select(ds => ds.SchoolId).ToHashSet();
            _context.DriverSchools.RemoveRange(existing.Where(ds => !desired.Contains(ds.SchoolId)));
            foreach (var schoolId in desired.Except(current))
                _context.DriverSchools.Add(new DriverSchool { DriverId = id, SchoolId = schoolId });

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpGet("search")]
        [Authorize(Roles = Roles.Consumer)]
        public async Task<ActionResult<IEnumerable<object>>> Search([FromQuery] int? schoolId, [FromQuery] double? lat, [FromQuery] double? lng, [FromQuery] double? radiusKm)
        {
            var hasGeo = lat.HasValue || lng.HasValue || radiusKm.HasValue;
            if (schoolId is <= 0 || (hasGeo &&
                (!lat.HasValue || !lng.HasValue || !radiusKm.HasValue ||
                 !double.IsFinite(lat.Value) || !double.IsFinite(lng.Value) || !double.IsFinite(radiusKm.Value) ||
                 lat.Value is < -90 or > 90 || lng.Value is < -180 or > 180 || radiusKm.Value is <= 0 or > 500)))
                return BadRequest("Informe latitude, longitude e raio válidos juntos (raio até 500 km).");
            var driversQuery = _context.Drivers.AsNoTracking().Where(d => d.IsActive);
            if (schoolId.HasValue)
                driversQuery = driversQuery.Where(d => _context.DriverSchools.Any(ds =>
                    ds.DriverId == d.Id && ds.SchoolId == schoolId.Value));
            var drivers = await driversQuery.Select(d => new
            {
                d.Id, d.Fullname, d.PhoneNumber, d.StudentCapacity, d.Latitude, d.Longitude,
                AvailableCapacity = d.StudentCapacity - _context.Students.Count(s => s.DriverId == d.Id),
                AverageRating = _context.Ratings.Where(r => r.DriverId == d.Id)
                    .Select(r => (double?)r.Score).Average()
            }).Where(d => d.AvailableCapacity > 0).ToListAsync();
            if (hasGeo)
                drivers = drivers.Where(d => d.Latitude.HasValue && d.Longitude.HasValue &&
                    DistanceInKm(lat!.Value, lng!.Value, d.Latitude.Value, d.Longitude.Value) <= radiusKm!.Value).ToList();
            var results = drivers.Select(d => new
            {
                d.Id, d.Fullname, d.PhoneNumber, d.StudentCapacity, d.AvailableCapacity, d.AverageRating
            });

            return Ok(results);
        }

        private static double DistanceInKm(double lat1, double lon1, double lat2, double lon2)
        {
            const double earthRadiusKm = 6371;
            var dLat = DegreesToRadians(lat2 - lat1);
            var dLon = DegreesToRadians(lon2 - lon1);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            a = Math.Clamp(a, 0, 1);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return earthRadiusKm * c;
        }

        private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180;
    }
}
