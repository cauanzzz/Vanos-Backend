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
    [Authorize(Roles = Roles.Consumer)]
    public class StudentsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public StudentsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Student>>> GetStudents([FromQuery] int? parentId)
        {
            if (parentId is not null)
            {
                if ((!User.IsInRole(Roles.Parent) && !User.IsInRole(Roles.Student)) || parentId != User.GetUserId())
                {
                    return Forbid();
                }

                return await _context.Students.Where(s => s.ParentId == parentId).ToListAsync();
            }

            var userId = User.GetUserId();
            return await _context.Students.AsNoTracking().Where(s => s.ParentId == userId).ToListAsync();
        }

        [HttpPost]
        [Authorize(Roles = Roles.Consumer)]
        public async Task<ActionResult<Student>> PostStudent(Student student)
        {
            var schoolExists = await _context.Schools.AnyAsync(s => s.Id == student.SchoolId);
            if (!schoolExists)
            {
                return BadRequest("Escola não encontrada. Verifique o SchoolId repassado.");
            }

            student.Id = 0;
            student.ParentId = User.GetUserId();
            student.DriverId = null;

            _context.Students.Add(student);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetStudents), new { id = student.Id }, student);
        }

        [HttpPatch("{id}/status")]
        public async Task<IActionResult> UpdateDailyStatus(int id, bool isGoingToday, bool isReturningToday)
        {
            var userId = User.GetUserId();
            var student = await _context.Students.SingleOrDefaultAsync(s => s.Id == id && s.ParentId == userId);

            if (student == null)
            {
                return NotFound("Aluno não encontrado.");
            }

            student.IsGoingToday = isGoingToday;
            student.IsReturningToday = isReturningToday;
            await _context.SaveChangesAsync();

            return Ok(student);
        }
    }
}
