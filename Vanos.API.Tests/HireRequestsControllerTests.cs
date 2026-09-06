using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Vanos.API.Controllers;
using Vanos.API.Data;
using Vanos.API.DTOs;
using Vanos.API.Models;

namespace Vanos.API.Tests
{
    public class HireRequestsControllerTests
    {
        private static HireRequestsController BuildController(AppDbContext context, ClaimsPrincipal user) =>
            new(context) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user } } };

        [Fact]
        public async Task Create_ForOwnUnassignedStudent_CreatesPendingRequest()
        {
            using var context = TestHelpers.BuildContext();
            context.Drivers.Add(new Driver { Id = 1, Fullname = "Cauan", CPF = "1", PhoneNumber = "1", LicensePlate = "X", StudentCapacity = 10, PixKey = "x" });
            context.Students.Add(new Student { Id = 1, FullName = "Lucas", ParentId = 10, SchoolId = 1, DriverId = null });
            await context.SaveChangesAsync();

            var controller = BuildController(context, TestHelpers.BuildParentPrincipal(10));

            var result = await controller.Create(new HireRequestCreateRequest { StudentId = 1, DriverId = 1 });

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var hireRequest = Assert.IsType<HireRequest>(okResult.Value);
            Assert.Equal(HireRequestStatus.Pending, hireRequest.Status);
        }

        [Fact]
        public async Task Create_ForStudentBelongingToAnotherParent_ReturnsForbid()
        {
            using var context = TestHelpers.BuildContext();
            context.Drivers.Add(new Driver { Id = 1, Fullname = "Cauan", CPF = "1", PhoneNumber = "1", LicensePlate = "X", StudentCapacity = 10, PixKey = "x" });
            context.Students.Add(new Student { Id = 1, FullName = "Lucas", ParentId = 10, SchoolId = 1, DriverId = null });
            await context.SaveChangesAsync();

            var controller = BuildController(context, TestHelpers.BuildParentPrincipal(999));

            var result = await controller.Create(new HireRequestCreateRequest { StudentId = 1, DriverId = 1 });

            Assert.IsType<ForbidResult>(result.Result);
        }

        [Fact]
        public async Task Create_WhenStudentAlreadyHasPendingRequest_ReturnsBadRequest()
        {
            using var context = TestHelpers.BuildContext();
            context.Drivers.Add(new Driver { Id = 1, Fullname = "Cauan", CPF = "1", PhoneNumber = "1", LicensePlate = "X", StudentCapacity = 10, PixKey = "x" });
            context.Drivers.Add(new Driver { Id = 2, Fullname = "Outro", CPF = "2", PhoneNumber = "2", LicensePlate = "Y", StudentCapacity = 10, PixKey = "y" });
            context.Students.Add(new Student { Id = 1, FullName = "Lucas", ParentId = 10, SchoolId = 1, DriverId = null });
            context.HireRequests.Add(new HireRequest { StudentId = 1, DriverId = 1, Status = HireRequestStatus.Pending });
            await context.SaveChangesAsync();

            var controller = BuildController(context, TestHelpers.BuildParentPrincipal(10));

            var result = await controller.Create(new HireRequestCreateRequest { StudentId = 1, DriverId = 2 });

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task Create_ForStudentWithAssignedDriver_ReturnsBadRequest()
        {
            using var context = TestHelpers.BuildContext();
            context.Drivers.Add(new Driver { Id = 1, Fullname = "Cauan", CPF = "1", PhoneNumber = "1", LicensePlate = "X", StudentCapacity = 10, PixKey = "x" });
            context.Students.Add(new Student { Id = 1, FullName = "Lucas", ParentId = 10, SchoolId = 1, DriverId = 1 });
            await context.SaveChangesAsync();

            var controller = BuildController(context, TestHelpers.BuildParentPrincipal(10));

            var result = await controller.Create(new HireRequestCreateRequest { StudentId = 1, DriverId = 1 });

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }
    }
}
