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

        [Fact]
        public async Task Accept_AssignsDriverToStudentAndRejectsOtherPendingRequests()
        {
            using var context = TestHelpers.BuildContext();
            context.Drivers.Add(new Driver { Id = 1, Fullname = "Cauan", CPF = "1", PhoneNumber = "1", LicensePlate = "X", StudentCapacity = 10, PixKey = "x" });
            context.Drivers.Add(new Driver { Id = 2, Fullname = "Outro", CPF = "2", PhoneNumber = "2", LicensePlate = "Y", StudentCapacity = 10, PixKey = "y" });
            context.Students.Add(new Student { Id = 1, FullName = "Lucas", ParentId = 10, SchoolId = 1, DriverId = null });
            var accepted = new HireRequest { Id = 1, StudentId = 1, DriverId = 1, Status = HireRequestStatus.Pending };
            var otherPending = new HireRequest { Id = 2, StudentId = 1, DriverId = 2, Status = HireRequestStatus.Pending };
            context.HireRequests.AddRange(accepted, otherPending);
            await context.SaveChangesAsync();

            var controller = BuildController(context, TestHelpers.BuildDriverPrincipal(1));

            var result = await controller.Accept(1);

            Assert.IsType<OkObjectResult>(result);
            var student = await context.Students.FindAsync(1);
            Assert.Equal(1, student!.DriverId);
            Assert.Equal(HireRequestStatus.Accepted, (await context.HireRequests.FindAsync(1))!.Status);
            Assert.Equal(HireRequestStatus.Rejected, (await context.HireRequests.FindAsync(2))!.Status);
        }

        [Fact]
        public async Task Accept_ForAnotherDriversRequest_ReturnsForbid()
        {
            using var context = TestHelpers.BuildContext();
            context.HireRequests.Add(new HireRequest { Id = 1, StudentId = 1, DriverId = 1, Status = HireRequestStatus.Pending });
            await context.SaveChangesAsync();

            var controller = BuildController(context, TestHelpers.BuildDriverPrincipal(2));

            var result = await controller.Accept(1);

            Assert.IsType<ForbidResult>(result);
        }

        [Fact]
        public async Task Accept_AlreadyRespondedRequest_ReturnsBadRequest()
        {
            using var context = TestHelpers.BuildContext();
            context.HireRequests.Add(new HireRequest { Id = 1, StudentId = 1, DriverId = 1, Status = HireRequestStatus.Rejected });
            await context.SaveChangesAsync();

            var controller = BuildController(context, TestHelpers.BuildDriverPrincipal(1));

            var result = await controller.Accept(1);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task Reject_SetsStatusToRejectedWithoutTouchingStudent()
        {
            using var context = TestHelpers.BuildContext();
            context.Students.Add(new Student { Id = 1, FullName = "Lucas", ParentId = 10, SchoolId = 1, DriverId = null });
            context.HireRequests.Add(new HireRequest { Id = 1, StudentId = 1, DriverId = 1, Status = HireRequestStatus.Pending });
            await context.SaveChangesAsync();

            var controller = BuildController(context, TestHelpers.BuildDriverPrincipal(1));

            var result = await controller.Reject(1);

            Assert.IsType<OkObjectResult>(result);
            Assert.Equal(HireRequestStatus.Rejected, (await context.HireRequests.FindAsync(1))!.Status);
            Assert.Null((await context.Students.FindAsync(1))!.DriverId);
        }

        [Fact]
        public async Task GetHireRequests_ByDriverId_ReturnsOnlyThatDriversRequests()
        {
            using var context = TestHelpers.BuildContext();
            context.HireRequests.Add(new HireRequest { Id = 1, StudentId = 1, DriverId = 1, Status = HireRequestStatus.Pending });
            context.HireRequests.Add(new HireRequest { Id = 2, StudentId = 2, DriverId = 2, Status = HireRequestStatus.Pending });
            await context.SaveChangesAsync();

            var controller = BuildController(context, TestHelpers.BuildDriverPrincipal(1));

            var result = await controller.GetHireRequests(driverId: 1, parentId: null);

            var requests = Assert.IsAssignableFrom<IEnumerable<HireRequest>>(result.Value);
            Assert.Single(requests);
        }

        [Fact]
        public async Task GetHireRequests_ByAnotherDriversId_ReturnsForbid()
        {
            using var context = TestHelpers.BuildContext();
            var controller = BuildController(context, TestHelpers.BuildDriverPrincipal(1));

            var result = await controller.GetHireRequests(driverId: 2, parentId: null);

            Assert.IsType<ForbidResult>(result.Result);
        }

        [Fact]
        public async Task GetHireRequests_ByParentId_ReturnsRequestsForTheirStudentsOnly()
        {
            using var context = TestHelpers.BuildContext();
            context.Students.Add(new Student { Id = 1, FullName = "Lucas", ParentId = 10, SchoolId = 1, DriverId = null });
            context.Students.Add(new Student { Id = 2, FullName = "Outro", ParentId = 20, SchoolId = 1, DriverId = null });
            context.HireRequests.Add(new HireRequest { Id = 1, StudentId = 1, DriverId = 1, Status = HireRequestStatus.Pending });
            context.HireRequests.Add(new HireRequest { Id = 2, StudentId = 2, DriverId = 1, Status = HireRequestStatus.Pending });
            await context.SaveChangesAsync();

            var controller = BuildController(context, TestHelpers.BuildParentPrincipal(10));

            var result = await controller.GetHireRequests(driverId: null, parentId: 10);

            var requests = Assert.IsAssignableFrom<IEnumerable<HireRequest>>(result.Value);
            Assert.Single(requests);
        }
    }
}
