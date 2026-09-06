using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Vanos.API.Models;
using Vanos.API.Services;

namespace Vanos.API.Tests
{
    public class JwtTokenServiceTests
    {
        private static IConfiguration BuildConfiguration() =>
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "unit-test-signing-key-with-at-least-32-chars",
                ["Jwt:Issuer"] = "VanosAPI.Tests",
                ["Jwt:Audience"] = "VanosApp.Tests"
            }).Build();

        [Fact]
        public void GenerateToken_IncludesUserIdAndRealRoleClaims()
        {
            var service = new JwtTokenService(BuildConfiguration());
            var user = new User { Id = 42, Email = "parent@example.com", Role = Roles.Parent };

            var token = service.GenerateToken(user);
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

            Assert.Equal("42", jwt.Claims.Single(c => c.Type == ClaimTypes.NameIdentifier).Value);
            Assert.Equal(Roles.Parent, jwt.Claims.Single(c => c.Type == ClaimTypes.Role).Value);
        }

        [Fact]
        public void GenerateToken_ForDriverUser_IncludesDriverIdClaim()
        {
            var service = new JwtTokenService(BuildConfiguration());
            var user = new User { Id = 7, Email = "driver@example.com", Role = Roles.Driver, DriverId = 99 };

            var token = service.GenerateToken(user);
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

            Assert.Equal("99", jwt.Claims.Single(c => c.Type == "driverId").Value);
        }

        [Fact]
        public void GenerateToken_ForParentUser_OmitsDriverIdClaim()
        {
            var service = new JwtTokenService(BuildConfiguration());
            var user = new User { Id = 7, Email = "parent@example.com", Role = Roles.Parent };

            var token = service.GenerateToken(user);
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

            Assert.DoesNotContain(jwt.Claims, c => c.Type == "driverId");
        }
    }
}
