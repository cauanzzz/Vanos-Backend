using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vanos.API.Data;
using Vanos.API.Models;

namespace Vanos.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MonthlyFeesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public MonthlyFeesController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<MonthlyFee>>> GetMonthlyFees()
        {
            return await _context.MonthlyFees.ToListAsync();
        }

        [HttpPost]
        public async Task<ActionResult<MonthlyFee>> PostMonthlyFee(MonthlyFee monthlyFee)
        {
            var studentExists = await _context.Students.AnyAsync(s => s.Id == monthlyFee.StudentId);
            var driverExists = await _context.Drivers.AnyAsync(d => d.Id == monthlyFee.DriverId);

            if (!studentExists || !driverExists)
            {
                return BadRequest("Aluno ou Motorista não encontrado. Verifique os IDs repassados.");
            }

            _context.MonthlyFees.Add(monthlyFee);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetMonthlyFees), new { id = monthlyFee.Id }, monthlyFee);
        }
    }
}