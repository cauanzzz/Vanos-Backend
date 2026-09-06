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
    }
}
