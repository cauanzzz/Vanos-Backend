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
    }
}
