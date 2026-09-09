using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Vanos.API.Controllers;
using Vanos.API.Data;
using Vanos.API.DTOs;
using Vanos.API.Models;

namespace Vanos.API.Tests
{
    public class RatingsControllerTests
    {
        private static RatingsController BuildController(AppDbContext context, ClaimsPrincipal user) =>
            new(context) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user } } };

        [Fact]
        public async Task Create_WithoutAcceptedHireRequest_ReturnsBadRequest()
        {
            using var context = TestHelpers.BuildContext();
            var controller = BuildController(context, TestHelpers.BuildParentPrincipal(10));

            var result = await controller.Create(new RatingCreateRequest { DriverId = 1, Score = 5 });

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task Create_WithAcceptedHireRequest_SavesRating()
        {
            using var context = TestHelpers.BuildContext();
            context.Students.Add(new Student { Id = 1, FullName = "Lucas", ParentId = 10, SchoolId = 1, DriverId = 1 });
            context.HireRequests.Add(new HireRequest { StudentId = 1, DriverId = 1, Status = HireRequestStatus.Accepted });
            await context.SaveChangesAsync();

            var controller = BuildController(context, TestHelpers.BuildParentPrincipal(10));

            var result = await controller.Create(new RatingCreateRequest { DriverId = 1, Score = 5, Comment = "Ótimo!" });

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var rating = Assert.IsType<Rating>(okResult.Value);
            Assert.Equal(5, rating.Score);
        }

        [Fact]
        public async Task Create_CalledTwice_UpsertsTheSameRatingRow()
        {
            using var context = TestHelpers.BuildContext();
            context.Students.Add(new Student { Id = 1, FullName = "Lucas", ParentId = 10, SchoolId = 1, DriverId = 1 });
            context.HireRequests.Add(new HireRequest { StudentId = 1, DriverId = 1, Status = HireRequestStatus.Accepted });
            await context.SaveChangesAsync();

            var controller = BuildController(context, TestHelpers.BuildParentPrincipal(10));
            await controller.Create(new RatingCreateRequest { DriverId = 1, Score = 3 });
            await controller.Create(new RatingCreateRequest { DriverId = 1, Score = 5 });

            var ratings = context.Ratings.Where(r => r.DriverId == 1 && r.ParentId == 10).ToList();
            Assert.Single(ratings);
            Assert.Equal(5, ratings[0].Score);
        }

        [Fact]
        public async Task Create_WithScoreOutOfRange_ReturnsBadRequest()
        {
            using var context = TestHelpers.BuildContext();
            var controller = BuildController(context, TestHelpers.BuildParentPrincipal(10));

            var result = await controller.Create(new RatingCreateRequest { DriverId = 1, Score = 6 });

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task GetForDriver_ReturnsAverageAndCount()
        {
            using var context = TestHelpers.BuildContext();
            context.Ratings.Add(new Rating { DriverId = 1, ParentId = 10, Score = 4 });
            context.Ratings.Add(new Rating { DriverId = 1, ParentId = 20, Score = 2 });
            await context.SaveChangesAsync();

            var controller = BuildController(context, TestHelpers.BuildParentPrincipal(10));

            var result = await controller.GetForDriver(1);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Equal(3.0, (double)okResult.Value!.GetType().GetProperty("Average")!.GetValue(okResult.Value)!);
            Assert.Equal(2, (int)okResult.Value!.GetType().GetProperty("Count")!.GetValue(okResult.Value)!);
        }
    }
}
