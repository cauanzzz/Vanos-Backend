using System.Data;
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
        [Authorize(Roles = Roles.Consumer)]
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

            var driverExists = await _context.Drivers.AnyAsync(d => d.Id == request.DriverId && d.IsActive);
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

        [HttpPatch("{id}/accept")]
        [Authorize(Roles = Roles.Driver)]
        public async Task<IActionResult> Accept(int id)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var hireRequest = await _context.HireRequests.FindAsync(id);
            if (hireRequest is null)
            {
                return NotFound("Solicitação não encontrada.");
            }

            if (hireRequest.DriverId != User.GetDriverId())
            {
                return Forbid();
            }

            if (hireRequest.Status != HireRequestStatus.Pending)
            {
                return BadRequest("Esta solicitação já foi respondida.");
            }

            var student = await _context.Students.FindAsync(hireRequest.StudentId);
            var driver = await _context.Drivers.FindAsync(hireRequest.DriverId);
            if (student is null || driver is null || !driver.IsActive)
                return NotFound("Aluno ou motorista ativo não encontrado.");
            if (student.DriverId.HasValue)
                return Conflict("Aluno já possui motorista.");
            var occupied = await _context.Students.CountAsync(s => s.DriverId == driver.Id);
            if (occupied >= driver.StudentCapacity)
                return Conflict("Van sem vagas disponíveis.");
            hireRequest.Status = HireRequestStatus.Accepted;
            hireRequest.RespondedAt = DateTime.UtcNow;
            student.DriverId = hireRequest.DriverId;

            var otherPendingRequests = await _context.HireRequests
                .Where(r => r.StudentId == hireRequest.StudentId && r.Id != hireRequest.Id && r.Status == HireRequestStatus.Pending)
                .ToListAsync();

            foreach (var other in otherPendingRequests)
            {
                other.Status = HireRequestStatus.Rejected;
                other.RespondedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return Ok(hireRequest);
        }

        [HttpPatch("{id}/reject")]
        [Authorize(Roles = Roles.Driver)]
        public async Task<IActionResult> Reject(int id)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var hireRequest = await _context.HireRequests.FindAsync(id);
            if (hireRequest is null)
            {
                return NotFound("Solicitação não encontrada.");
            }

            if (hireRequest.DriverId != User.GetDriverId())
            {
                return Forbid();
            }

            if (hireRequest.Status != HireRequestStatus.Pending)
            {
                return BadRequest("Esta solicitação já foi respondida.");
            }

            hireRequest.Status = HireRequestStatus.Rejected;
            hireRequest.RespondedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();
            return Ok(hireRequest);
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<HireRequest>>> GetHireRequests([FromQuery] int? driverId, [FromQuery] int? parentId)
        {
            if (driverId is not null)
            {
                if (!User.IsInRole(Roles.Driver) || driverId != User.GetDriverId())
                {
                    return Forbid();
                }

                return await _context.HireRequests.Where(r => r.DriverId == driverId).ToListAsync();
            }

            if (parentId is not null)
            {
                if ((!User.IsInRole(Roles.Parent) && !User.IsInRole(Roles.Student)) || parentId != User.GetUserId())
                {
                    return Forbid();
                }

                var studentIds = await _context.Students
                    .Where(s => s.ParentId == parentId)
                    .Select(s => s.Id)
                    .ToListAsync();

                return await _context.HireRequests.Where(r => studentIds.Contains(r.StudentId)).ToListAsync();
            }

            return BadRequest("Informe driverId ou parentId.");
        }
    }
}
