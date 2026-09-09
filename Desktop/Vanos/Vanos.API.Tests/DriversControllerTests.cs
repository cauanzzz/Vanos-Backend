using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Vanos.API.Controllers;
using Vanos.API.Models;

namespace Vanos.API.Tests
{
    public class DriversControllerTests
    {
        [Fact]
        public async Task UpdateSchoolsServed_ReplacesExistingSchoolsForOwnDriver()
        {
            using var context = TestHelpers.BuildContext();
            context.Drivers.Add(new Driver { Id = 1, Fullname = "Cauan", CPF = "1", PhoneNumber = "1", LicensePlate = "X", StudentCapacity = 10, PixKey = "x" });
            context.Schools.AddRange(new School { Id = 5 }, new School { Id = 7 });
            context.DriverSchools.Add(new DriverSchool { DriverId = 1, SchoolId = 99 });
            await context.SaveChangesAsync();

            var controller = new DriversController(context)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = TestHelpers.BuildDriverPrincipal(1) } }
            };

            var result = await controller.UpdateSchoolsServed(1, new List<int> { 5, 7 });

            Assert.IsType<NoContentResult>(result);
            var schoolIds = context.DriverSchools.Where(ds => ds.DriverId == 1).Select(ds => ds.SchoolId).OrderBy(x => x).ToList();
            Assert.Equal(new[] { 5, 7 }, schoolIds);
        }

        [Fact]
        public async Task UpdateSchoolsServed_ForAnotherDriversId_ReturnsForbid()
        {
            using var context = TestHelpers.BuildContext();

            var controller = new DriversController(context)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = TestHelpers.BuildDriverPrincipal(1) } }
            };

            var result = await controller.UpdateSchoolsServed(2, new List<int> { 5 });

            Assert.IsType<ForbidResult>(result);
        }

        [Fact]
        public async Task Search_BySchoolId_ExcludesDriversNotServingThatSchool()
        {
            using var context = TestHelpers.BuildContext();
            context.Drivers.Add(new Driver { Id = 1, Fullname = "Serve", CPF = "1", PhoneNumber = "1", LicensePlate = "X", StudentCapacity = 10, PixKey = "x", IsActive = true });
            context.Drivers.Add(new Driver { Id = 2, Fullname = "NaoServe", CPF = "2", PhoneNumber = "2", LicensePlate = "Y", StudentCapacity = 10, PixKey = "y", IsActive = true });
            context.DriverSchools.Add(new DriverSchool { DriverId = 1, SchoolId = 5 });
            await context.SaveChangesAsync();

            var controller = new DriversController(context)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = TestHelpers.BuildParentPrincipal(10) } }
            };

            var result = await controller.Search(schoolId: 5, lat: null, lng: null, radiusKm: null);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var results = Assert.IsAssignableFrom<IEnumerable<object>>(okResult.Value);
            Assert.Single(results);
        }

        [Fact]
        public async Task Search_ByLocation_ExcludesDriversOutsideRadius()
        {
            using var context = TestHelpers.BuildContext();
            context.Drivers.Add(new Driver { Id = 1, Fullname = "Perto", CPF = "1", PhoneNumber = "1", LicensePlate = "X", StudentCapacity = 10, PixKey = "x", IsActive = true, Latitude = -22.90, Longitude = -47.06 });
            context.Drivers.Add(new Driver { Id = 2, Fullname = "Longe", CPF = "2", PhoneNumber = "2", LicensePlate = "Y", StudentCapacity = 10, PixKey = "y", IsActive = true, Latitude = -23.55, Longitude = -46.63 });
            await context.SaveChangesAsync();

            var controller = new DriversController(context)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = TestHelpers.BuildParentPrincipal(10) } }
            };

            var result = await controller.Search(schoolId: null, lat: -22.90, lng: -47.06, radiusKm: 10);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var results = Assert.IsAssignableFrom<IEnumerable<object>>(okResult.Value);
            Assert.Single(results);
        }

        [Fact]
        public async Task Search_ComputesAvailableCapacityFromCurrentStudentAssignments()
        {
            using var context = TestHelpers.BuildContext();
            context.Drivers.Add(new Driver { Id = 1, Fullname = "Cauan", CPF = "1", PhoneNumber = "1", LicensePlate = "X", StudentCapacity = 10, PixKey = "x", IsActive = true });
            context.Students.Add(new Student { Id = 1, DriverId = 1 });
            context.HireRequests.Add(new HireRequest { StudentId = 1, DriverId = 1, Status = HireRequestStatus.Accepted });
            context.HireRequests.Add(new HireRequest { StudentId = 2, DriverId = 1, Status = HireRequestStatus.Pending });
            await context.SaveChangesAsync();

            var controller = new DriversController(context)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = TestHelpers.BuildParentPrincipal(10) } }
            };

            var result = await controller.Search(schoolId: null, lat: null, lng: null, radiusKm: null);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var results = Assert.IsAssignableFrom<IEnumerable<object>>(okResult.Value).ToList();
            var first = results.Single();
            Assert.Equal(9, (int)first.GetType().GetProperty("AvailableCapacity")!.GetValue(first)!);
        }
    }
}
