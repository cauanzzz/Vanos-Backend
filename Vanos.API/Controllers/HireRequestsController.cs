using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vanos.API.Data;
using Vanos.API.DTOs;
using Vanos.API.Extensions;
using Vanos.API.Models;

namespace Vanos.API.Controllers
{
    [Route("api/hirerequests")]
    [ApiController]
    [Authorize]
    public class HireRequestsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public HireRequestsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        [Authorize(Roles = Roles.Parent)]
        public async Task<ActionResult<HireRequest>> Create(HireRequestCreateRequest request)
        {
            var student = await _context.Students.FindAsync(request.StudentId);
            if (student is null)
            {
                return NotFound("Aluno não encontrado.");
            }

            if (student.ParentId != User.GetUserId())
            {
                return Forbid();
            }

            if (student.DriverId.HasValue)
            {
                return BadRequest("Este aluno já possui um motorista.");
            }

            var hasPendingRequest = await _context.HireRequests
                .AnyAsync(r => r.StudentId == request.StudentId && r.Status == HireRequestStatus.Pending);

            if (hasPendingRequest)
            {
                return BadRequest("Este aluno já possui uma solicitação pendente.");
            }

            var driverExists = await _context.Drivers.AnyAsync(d => d.Id == request.DriverId);
            if (!driverExists)
            {
                return BadRequest("Motorista não encontrado.");
            }

            var hireRequest = new HireRequest
            {
                StudentId = request.StudentId,
                DriverId = request.DriverId
            };

            _context.HireRequests.Add(hireRequest);
            await _context.SaveChangesAsync();

            return Ok(hireRequest);
        }
    }
}
