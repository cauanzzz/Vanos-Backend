using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Vanos.API.Controllers;
using Vanos.API.Data;
using Vanos.API.Models;

namespace Vanos.API.Tests
{
    public class StudentsControllerTests
    {
        private static StudentsController BuildController(AppDbContext context, ClaimsPrincipal user) =>
            new(context) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user } } };

        [Fact]
        public async Task PostStudent_SetsParentIdFromJwtAndLeavesDriverUnassigned()
        {
            using var context = TestHelpers.BuildContext();
            context.Schools.Add(new School { Id = 1, Name = "PUC-Campinas" });
            await context.SaveChangesAsync();

            var controller = BuildController(context, TestHelpers.BuildParentPrincipal(10));

            var result = await controller.PostStudent(new Student { FullName = "Lucas Silva", SchoolId = 1, DriverId = 999 });

            var created = Assert.IsType<CreatedAtActionResult>(result.Result);
            var student = Assert.IsType<Student>(created.Value);
            Assert.Equal(10, student.ParentId);
            Assert.Null(student.DriverId);
        }

        [Fact]
        public async Task PostStudent_WithUnknownSchool_ReturnsBadRequest()
        {
            using var context = TestHelpers.BuildContext();
            var controller = BuildController(context, TestHelpers.BuildParentPrincipal(10));

            var result = await controller.PostStudent(new Student { FullName = "Lucas Silva", SchoolId = 999 });

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }
    }
}
