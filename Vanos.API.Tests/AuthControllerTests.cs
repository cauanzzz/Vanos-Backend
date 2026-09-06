using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Vanos.API.Controllers;
using Vanos.API.Data;
using Vanos.API.DTOs;
using Vanos.API.Models;
using Vanos.API.Services;

namespace Vanos.API.Tests
{
    public class AuthControllerTests
    {
        private static IConfiguration BuildConfiguration() =>
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "unit-test-signing-key-with-at-least-32-chars",
                ["Jwt:Issuer"] = "VanosAPI.Tests",
                ["Jwt:Audience"] = "VanosApp.Tests"
            }).Build();

        private static AuthController BuildController(AppDbContext context) =>
            new(context, new PasswordHasher(), new JwtTokenService(BuildConfiguration()));

        [Fact]
        public async Task Register_WithParentRole_CreatesUserWithoutDriverProfile()
        {
            using var context = TestHelpers.BuildContext();
            var controller = BuildController(context);

            var result = await controller.Register(new RegisterRequest
            {
                Email = "parent@example.com",
                Password = "Sup3rSecret!",
                Role = Roles.Parent
            });

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<AuthResponse>(okResult.Value);
            Assert.Equal(Roles.Parent, response.Role);
            Assert.Empty(context.Drivers);
            Assert.Single(context.Users);
        }

        [Fact]
        public async Task Register_WithDriverRole_CreatesLinkedDriverProfile()
        {
            using var context = TestHelpers.BuildContext();
            var controller = BuildController(context);

            var result = await controller.Register(new RegisterRequest
            {
                Email = "driver@example.com",
                Password = "Sup3rSecret!",
                Role = Roles.Driver,
                Fullname = "Cauan Braga",
                CPF = "123.456.789-00",
                PhoneNumber = "19 98888-7777",
                LicensePlate = "VAN-2026",
                StudentCapacity = 15,
                PixKey = "cauan@email.com"
            });

            Assert.IsType<OkObjectResult>(result.Result);
            var driver = Assert.Single(context.Drivers);
            var user = Assert.Single(context.Users);
            Assert.Equal(driver.Id, user.DriverId);
        }

        [Fact]
        public async Task Register_WithDuplicateEmail_ReturnsBadRequest()
        {
            using var context = TestHelpers.BuildContext();
            var controller = BuildController(context);
            await controller.Register(new RegisterRequest { Email = "dup@example.com", Password = "Sup3rSecret!", Role = Roles.Parent });

            var result = await controller.Register(new RegisterRequest { Email = "dup@example.com", Password = "Other123!", Role = Roles.Parent });

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task Register_WithDriverRoleAndMissingProfileFields_ReturnsBadRequest()
        {
            using var context = TestHelpers.BuildContext();
            var controller = BuildController(context);

            var result = await controller.Register(new RegisterRequest
            {
                Email = "driver@example.com",
                Password = "Sup3rSecret!",
                Role = Roles.Driver
            });

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }
    }
}
