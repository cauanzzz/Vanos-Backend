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
    public class DriversController : ControllerBase
    {
        private readonly AppDbContext _context;

        public DriversController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Driver>>> GetDrivers()
        {
            return await _context.Drivers.ToListAsync();
        }

        [HttpPost]
        public async Task<ActionResult<Driver>> PostDriver(Driver driver)
        {
            _context.Drivers.Add(driver);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetDrivers), new { id = driver.Id }, driver);
        }

        [HttpGet("{id}/students")]
        public async Task<ActionResult<IEnumerable<Student>>> GetStudentsByDriver(int id)
        {
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

            var existing = _context.DriverSchools.Where(ds => ds.DriverId == id);
            _context.DriverSchools.RemoveRange(existing);

            foreach (var schoolId in schoolIds.Distinct())
            {
                _context.DriverSchools.Add(new DriverSchool { DriverId = id, SchoolId = schoolId });
            }

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpGet("search")]
        [Authorize(Roles = Roles.Parent)]
        public async Task<ActionResult<IEnumerable<object>>> Search([FromQuery] int? schoolId, [FromQuery] double? lat, [FromQuery] double? lng, [FromQuery] double? radiusKm)
        {
            var driversQuery = _context.Drivers.Where(d => d.IsActive);

            if (schoolId is not null)
            {
                var driverIdsForSchool = _context.DriverSchools
                    .Where(ds => ds.SchoolId == schoolId)
                    .Select(ds => ds.DriverId);

                driversQuery = driversQuery.Where(d => driverIdsForSchool.Contains(d.Id));
            }

            var drivers = await driversQuery.ToListAsync();

            if (lat is not null && lng is not null && radiusKm is not null)
            {
                drivers = drivers
                    .Where(d => d.Latitude is not null && d.Longitude is not null &&
                                DistanceInKm(lat.Value, lng.Value, d.Latitude.Value, d.Longitude.Value) <= radiusKm.Value)
                    .ToList();
            }

            var results = new List<object>();
            foreach (var driver in drivers)
            {
                var acceptedStudentCount = await _context.HireRequests
                    .Where(r => r.DriverId == driver.Id && r.Status == HireRequestStatus.Accepted)
                    .CountAsync();

                var ratings = await _context.Ratings.Where(r => r.DriverId == driver.Id).ToListAsync();

                results.Add(new
                {
                    driver.Id,
                    driver.Fullname,
                    driver.PhoneNumber,
                    driver.StudentCapacity,
                    AvailableCapacity = driver.StudentCapacity - acceptedStudentCount,
                    AverageRating = ratings.Count == 0 ? (double?)null : ratings.Average(r => r.Score)
                });
            }

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

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return earthRadiusKm * c;
        }

        private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180;
    }
}
