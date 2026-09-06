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
        public async Task Register_WithDriverRoleAndLocation_PersistsLatitudeAndLongitude()
        {
            using var context = TestHelpers.BuildContext();
            var controller = BuildController(context);

            var result = await controller.Register(new RegisterRequest
            {
                Email = "driver-loc@example.com",
                Password = "Sup3rSecret!",
                Role = Roles.Driver,
                Fullname = "Cauan Braga",
                CPF = "123.456.789-00",
                PhoneNumber = "19 98888-7777",
                LicensePlate = "VAN-2026",
                StudentCapacity = 15,
                PixKey = "cauan@email.com",
                Latitude = -22.90,
                Longitude = -47.06
            });

            Assert.IsType<OkObjectResult>(result.Result);
            var driver = Assert.Single(context.Drivers);
            Assert.Equal(-22.90, driver.Latitude);
            Assert.Equal(-47.06, driver.Longitude);
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

        [Fact]
        public async Task Login_WithCorrectPassword_ReturnsTokenWithRealRole()
        {
            using var context = TestHelpers.BuildContext();
            var controller = BuildController(context);
            await controller.Register(new RegisterRequest { Email = "parent@example.com", Password = "Sup3rSecret!", Role = Roles.Parent });

            var result = await controller.Login(new LoginRequest { Email = "parent@example.com", Password = "Sup3rSecret!" });

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<AuthResponse>(okResult.Value);
            Assert.Equal(Roles.Parent, response.Role);
        }

        [Fact]
        public async Task Login_WithWrongPassword_ReturnsUnauthorized()
        {
            using var context = TestHelpers.BuildContext();
            var controller = BuildController(context);
            await controller.Register(new RegisterRequest { Email = "parent@example.com", Password = "Sup3rSecret!", Role = Roles.Parent });

            var result = await controller.Login(new LoginRequest { Email = "parent@example.com", Password = "WrongPassword" });

            Assert.IsType<UnauthorizedObjectResult>(result.Result);
        }

        [Fact]
        public async Task Login_WithUnknownEmail_ReturnsUnauthorized()
        {
            using var context = TestHelpers.BuildContext();
            var controller = BuildController(context);

            var result = await controller.Login(new LoginRequest { Email = "nobody@example.com", Password = "Sup3rSecret!" });

            Assert.IsType<UnauthorizedObjectResult>(result.Result);
        }
    }
}
