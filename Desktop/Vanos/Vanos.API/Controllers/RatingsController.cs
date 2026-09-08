using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vanos.API.Data;
using Vanos.API.DTOs;
using Vanos.API.Extensions;
using Vanos.API.Models;

namespace Vanos.API.Controllers
{
    [Route("api/ratings")]
    [ApiController]
    [Authorize]
    public class RatingsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public RatingsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        [Authorize(Roles = Roles.Consumer)]
        public async Task<ActionResult<Rating>> Create(RatingCreateRequest request)
        {
            if (request.Score is < 1 or > 5)
            {
                return BadRequest("Score deve estar entre 1 e 5.");
            }

            var parentId = User.GetUserId();

            var hasAcceptedHireRequest = await _context.HireRequests
                .Where(r => r.DriverId == request.DriverId && r.Status == HireRequestStatus.Accepted)
                .Join(_context.Students, r => r.StudentId, s => s.Id, (r, s) => s)
                .AnyAsync(s => s.ParentId == parentId);

            if (!hasAcceptedHireRequest)
            {
                return BadRequest("Você só pode avaliar motoristas que já contratou.");
            }

            var rating = await _context.Ratings
                .SingleOrDefaultAsync(r => r.DriverId == request.DriverId && r.ParentId == parentId);

            if (rating is null)
            {
                rating = new Rating { DriverId = request.DriverId, ParentId = parentId };
                _context.Ratings.Add(rating);
            }

            rating.Score = request.Score;
            rating.Comment = request.Comment;
            rating.CreatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok(rating);
        }

        [HttpGet("/api/drivers/{driverId}/ratings")]
        public async Task<ActionResult<object>> GetForDriver(int driverId)
        {
            var ratings = await _context.Ratings.Where(r => r.DriverId == driverId).ToListAsync();

            return Ok(new
            {
                Average = ratings.Count == 0 ? (double?)null : ratings.Average(r => r.Score),
                Count = ratings.Count,
                Ratings = ratings
            });
        }
    }
}
