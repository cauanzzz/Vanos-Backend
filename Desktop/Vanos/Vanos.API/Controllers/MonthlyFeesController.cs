using System.Linq.Expressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Vanos.API.Data;
using Vanos.API.DTOs;
using Vanos.API.Extensions;
using Vanos.API.Models;
namespace Vanos.API.Controllers;

[ApiController]
[Route("api/monthlyfees")]
[Authorize]
public sealed class MonthlyFeesController(AppDbContext db, IWebHostEnvironment environment,
    IConfiguration configuration) : ControllerBase
{
    private static readonly Expression<Func<MonthlyFee, MonthlyFeeResponse>> Projection = f =>
        new MonthlyFeeResponse(f.Id, f.StudentId, f.DriverId, f.Amount, f.DueDate,
            f.IsPaid, f.PaymentDate, f.IsSimulated);

    private IQueryable<MonthlyFee> OwnedFees()
    {
        var id = User.GetUserId();
        return db.MonthlyFees.Where(f => db.Students.Any(s => s.Id == f.StudentId && s.ParentId == id));
    }

    [HttpGet]
    [HttpGet("mine")]
    [Authorize(Roles = Roles.Consumer)]
    public async Task<ActionResult<PageResult<MonthlyFeeResponse>>> GetMine(
        [FromQuery] bool pendingOnly = true, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        if (page is < 1 or > 100000 || pageSize is < 1 or > 100)
            return BadRequest("Paginação inválida.");
        var query = OwnedFees().AsNoTracking();
        if (pendingOnly) query = query.Where(f => !f.IsPaid);
        var rows = await query.OrderBy(f => f.DueDate).ThenBy(f => f.Id)
            .Skip((page - 1) * pageSize).Take(pageSize + 1).Select(Projection).ToListAsync(ct);
        return Ok(new PageResult<MonthlyFeeResponse>(page, pageSize, rows.Count > pageSize,
            rows.Take(pageSize).ToArray()));
    }

    [HttpPost]
    [Authorize(Roles = Roles.Driver)]
    public async Task<IActionResult> Create(MonthlyFeeCreateRequest request, CancellationToken ct = default)
    {
        if (request.StudentId <= 0 || request.Amount is <= 0 or > 999999.99m ||
            decimal.Round(request.Amount, 2) != request.Amount || request.DueDate == default)
            return BadRequest("Informe aluno, vencimento e valor positivo com até duas casas decimais.");
        var driverId = User.GetDriverId();
        if (!await db.Students.AnyAsync(s => s.Id == request.StudentId && s.DriverId == driverId, ct) ||
            !await db.Drivers.AnyAsync(d => d.Id == driverId && d.IsActive, ct))
            return NotFound("Vínculo ativo não encontrado.");
        var dueDate = request.DueDate.Date;
        if (await db.MonthlyFees.AnyAsync(f => f.StudentId == request.StudentId &&
            f.DriverId == driverId && f.DueDate == dueDate, ct))
            return Conflict("Fatura já cadastrada para esse vencimento.");
        var fee = new MonthlyFee { StudentId = request.StudentId, DriverId = driverId,
            Amount = request.Amount, DueDate = dueDate, IsPaid = false, IsSimulated = false };
        db.MonthlyFees.Add(fee);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql &&
            (sql.Number == 2601 || sql.Number == 2627))
        { return Conflict("Fatura já cadastrada para esse vencimento."); }
        return StatusCode(StatusCodes.Status201Created, new MonthlyFeeResponse(fee.Id,
            fee.StudentId, fee.DriverId, fee.Amount, fee.DueDate, false, null, false));
    }

    [HttpPost("{id:int}/simulate-payment")]
    [Authorize(Roles = Roles.Consumer)]
    public async Task<ActionResult<MonthlyFeeResponse>> SimulatePayment(int id, CancellationToken ct = default)
    {
        if (!environment.IsDevelopment() || !configuration.GetValue<bool>("Payments:EnableSimulation"))
            return NotFound();
        var query = OwnedFees().Where(f => f.Id == id);
        var now = DateTime.UtcNow;
        // One conditional UPDATE; retries never overwrite an existing payment date.
        await query.Where(f => !f.IsPaid).ExecuteUpdateAsync(setters => setters
            .SetProperty(f => f.IsPaid, true)
            .SetProperty(f => f.PaymentDate, (DateTime?)now)
            .SetProperty(f => f.IsSimulated, true), ct);
        var result = await query.AsNoTracking().Select(Projection).SingleOrDefaultAsync(ct);
        if (result is null) return NotFound();
        return Ok(result);
    }
}
