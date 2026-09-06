# Driver Matching (Discovery, Hire Requests, Ratings) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let parents register, add students, search for drivers by school/location, send hire requests, and rate a driver after being hired — replacing the current assumption that a driver/family relationship already exists before a student record is created.

**Architecture:** Add three new EF Core entities (`DriverSchool`, `HireRequest`, `Rating`) and two nullable/new columns (`Student.DriverId` becomes optional, `Student.ParentId` and `Driver.Latitude`/`Longitude` are added). Extract password hashing and JWT issuance out of `AuthController` into two small, independently-testable services. Every new/changed write endpoint reads the caller's identity (user id, and driver id when applicable) from JWT claims rather than trusting a client-supplied id, so ownership is enforced server-side.

**Tech Stack:** ASP.NET Core 10 (Web API), EF Core 10 + SQL Server (dev), JWT Bearer auth, BCrypt.Net-Next for password hashing, xUnit + EF Core InMemory provider for tests.

**Spec:** `docs/superpowers/specs/2026-09-05-driver-matching-design.md`

## Global Constraints

- Work directly on `master` — no feature branches, per user instruction.
- All new/changed endpoints require a valid JWT except `/api/auth/register` and `/api/auth/login` (per spec's Auth & roles section).
- Ownership (a driver acting on their own `DriverId`, a parent on their own students) is always taken from JWT claims, never from a client-supplied path/query parameter (per spec's Authorization rules).
- A student can have at most one `Pending` `HireRequest` at a time (per spec's Business rules).
- A `Rating` requires a pre-existing `Accepted` `HireRequest` between that parent and driver, and is an upsert keyed on (`DriverId`, `ParentId`) (per spec's schema section).
- Endpoints not listed in the spec's endpoint table (e.g. existing `GET /api/students`, `GET /api/drivers`, `POST /api/drivers`, `POST /api/schools`, `MonthlyFees` endpoints) are out of scope — do not add authorization to them as part of this plan.
- Live GPS tracking, routes/trips, and payment processing are explicitly out of scope (per spec's Non-goals).

---

## File Structure

New files:
- `Vanos.API.Tests/Vanos.API.Tests.csproj` — test project
- `Vanos.API.Tests/TestHelpers.cs` — shared `BuildContext()`/`BuildDriverPrincipal()`/`BuildParentPrincipal()` helpers used across test classes
- `Vanos.API.Tests/PasswordHasherTests.cs`
- `Vanos.API.Tests/JwtTokenServiceTests.cs`
- `Vanos.API.Tests/AuthControllerTests.cs`
- `Vanos.API.Tests/StudentModelTests.cs`
- `Vanos.API.Tests/DriversControllerTests.cs`
- `Vanos.API.Tests/HireRequestsControllerTests.cs`
- `Vanos.API.Tests/StudentsControllerTests.cs`
- `Vanos.API.Tests/RatingsControllerTests.cs`
- `Vanos.API/Services/IPasswordHasher.cs`, `Vanos.API/Services/PasswordHasher.cs`
- `Vanos.API/Services/IJwtTokenService.cs`, `Vanos.API/Services/JwtTokenService.cs`
- `Vanos.API/Extensions/ClaimsPrincipalExtensions.cs`
- `Vanos.API/Models/Roles.cs`
- `Vanos.API/Models/DriverSchool.cs`
- `Vanos.API/Models/HireRequest.cs` (includes `HireRequestStatus` enum)
- `Vanos.API/Models/Rating.cs`
- `Vanos.API/DTOs/RegisterRequest.cs`, `Vanos.API/DTOs/LoginRequest.cs`, `Vanos.API/DTOs/AuthResponse.cs`
- `Vanos.API/DTOs/HireRequestCreateRequest.cs`
- `Vanos.API/DTOs/RatingCreateRequest.cs`
- `Vanos.API/Controllers/HireRequestsController.cs`
- `Vanos.API/Controllers/RatingsController.cs`
- `.gitignore` (repo root)

Modified files:
- `Vanos.API/Models/Student.cs` — `DriverId` becomes `int?`, adds `ParentId`
- `Vanos.API/Models/Driver.cs` — adds `Latitude`/`Longitude`
- `Vanos.API/Data/AppDbContext.cs` — new `DbSet`s, `OnModelCreating` for composite/unique keys
- `Vanos.API/Controllers/AuthController.cs` — full rewrite (Register + fixed Login)
- `Vanos.API/Controllers/StudentsController.cs` — `PostStudent` requires `ParentId` from JWT
- `Vanos.API/Controllers/DriversController.cs` — adds `UpdateSchoolsServed` and `Search`
- `Vanos.API/Program.cs` — DI registrations, `AddAuthorization`, `UseAuthorization`
- `Vanos.API/Vanos.API.csproj` — adds `BCrypt.Net-Next` package
- `Vanos.API/Vanos.API.http` — extends with the full matching flow

---

### Task 1: Test project scaffolding + `.gitignore`

**Files:**
- Create: `.gitignore`
- Create: `Vanos.API.Tests/Vanos.API.Tests.csproj`
- Create: `Vanos.API.Tests/TestHelpers.cs`
- Create: `Vanos.API.Tests/SmokeTests.cs`

**Interfaces:**
- Produces: `TestHelpers.BuildContext()` → `AppDbContext` backed by a fresh EF Core InMemory database (unique name per call), used by every later test class in this plan.

- [ ] **Step 1: Add a `.gitignore` so build output never gets committed**

```
bin/
obj/
```

- [ ] **Step 2: Create the test project**

Run from the repo root (`Vanos-Backend/`):
```bash
dotnet new xunit -n Vanos.API.Tests -o Vanos.API.Tests
dotnet add Vanos.API.Tests/Vanos.API.Tests.csproj reference Vanos.API/Vanos.API.csproj
dotnet add Vanos.API.Tests/Vanos.API.Tests.csproj package Microsoft.EntityFrameworkCore.InMemory --version 10.0.9
```

- [ ] **Step 3: Write `TestHelpers.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using Vanos.API.Data;

namespace Vanos.API.Tests
{
    public static class TestHelpers
    {
        public static AppDbContext BuildContext() =>
            new(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
    }
}
```

- [ ] **Step 4: Write a smoke test to confirm the harness works**

```csharp
using Vanos.API.Models;

namespace Vanos.API.Tests
{
    public class SmokeTests
    {
        [Fact]
        public async Task Context_CanSaveAndReadASchool()
        {
            using var context = TestHelpers.BuildContext();
            context.Schools.Add(new School { Name = "PUC-Campinas" });
            await context.SaveChangesAsync();

            Assert.Single(context.Schools);
        }
    }
}
```

- [ ] **Step 5: Run the test to confirm it passes**

Run: `dotnet test Vanos.API.Tests`
Expected: PASS (1 test)

- [ ] **Step 6: Commit**

```bash
git add .gitignore Vanos.API.Tests
git commit -m "test: scaffold Vanos.API.Tests with EF Core InMemory provider"
```

---

### Task 2: `PasswordHasher` service

**Files:**
- Modify: `Vanos.API/Vanos.API.csproj`
- Create: `Vanos.API/Services/IPasswordHasher.cs`
- Create: `Vanos.API/Services/PasswordHasher.cs`
- Test: `Vanos.API.Tests/PasswordHasherTests.cs`

**Interfaces:**
- Produces: `IPasswordHasher.Hash(string password) : string`, `IPasswordHasher.Verify(string password, string hash) : bool` — consumed by `AuthController` in Task 4/5.

- [ ] **Step 1: Add the BCrypt package**

```bash
dotnet add Vanos.API/Vanos.API.csproj package BCrypt.Net-Next --version 4.0.3
```

- [ ] **Step 2: Write the failing test**

```csharp
using Vanos.API.Services;

namespace Vanos.API.Tests
{
    public class PasswordHasherTests
    {
        private readonly PasswordHasher _hasher = new();

        [Fact]
        public void Hash_ProducesHashThatVerifiesAgainstOriginalPassword()
        {
            var hash = _hasher.Hash("Sup3rSecret!");

            Assert.True(_hasher.Verify("Sup3rSecret!", hash));
        }

        [Fact]
        public void Verify_ReturnsFalseForWrongPassword()
        {
            var hash = _hasher.Hash("Sup3rSecret!");

            Assert.False(_hasher.Verify("WrongPassword", hash));
        }
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test Vanos.API.Tests --filter PasswordHasherTests`
Expected: FAIL (compile error — `PasswordHasher` doesn't exist yet)

- [ ] **Step 4: Write `IPasswordHasher.cs` and `PasswordHasher.cs`**

```csharp
namespace Vanos.API.Services
{
    public interface IPasswordHasher
    {
        string Hash(string password);
        bool Verify(string password, string hash);
    }
}
```

```csharp
namespace Vanos.API.Services
{
    public class PasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password);

        public bool Verify(string password, string hash) => BCrypt.Net.BCrypt.Verify(password, hash);
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test Vanos.API.Tests --filter PasswordHasherTests`
Expected: PASS (2 tests)

- [ ] **Step 6: Commit**

```bash
git add Vanos.API/Vanos.API.csproj Vanos.API/Services Vanos.API.Tests/PasswordHasherTests.cs
git commit -m "feat: add PasswordHasher service backed by BCrypt"
```

---

### Task 3: `JwtTokenService`

**Files:**
- Create: `Vanos.API/Services/IJwtTokenService.cs`
- Create: `Vanos.API/Services/JwtTokenService.cs`
- Create: `Vanos.API/Models/Roles.cs`
- Test: `Vanos.API.Tests/JwtTokenServiceTests.cs`

**Interfaces:**
- Consumes: `User` (existing model — `Id`, `Email`, `Role`, `DriverId`).
- Produces: `IJwtTokenService.GenerateToken(User user) : string`, consumed by `AuthController` in Task 4/5. Emits claims `ClaimTypes.NameIdentifier` (user id), `JwtRegisteredClaimNames.Sub` (email), `ClaimTypes.Role` (real role), and `"driverId"` (only when `user.DriverId.HasValue`) — `"driverId"` is read by `ClaimsPrincipalExtensions.GetDriverId()` in Task 7 onward.
- Produces: `Roles.Driver = "Driver"`, `Roles.Parent = "Parent"` constants, used by every controller/test from here on.

- [ ] **Step 1: Write `Roles.cs`**

```csharp
namespace Vanos.API.Models
{
    public static class Roles
    {
        public const string Driver = "Driver";
        public const string Parent = "Parent";
    }
}
```

- [ ] **Step 2: Write the failing tests**

```csharp
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
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test Vanos.API.Tests --filter JwtTokenServiceTests`
Expected: FAIL (compile error — `JwtTokenService`, `IJwtTokenService` don't exist yet)

- [ ] **Step 4: Write `IJwtTokenService.cs` and `JwtTokenService.cs`**

```csharp
using Vanos.API.Models;

namespace Vanos.API.Services
{
    public interface IJwtTokenService
    {
        string GenerateToken(User user);
    }
}
```

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Vanos.API.Models;

namespace Vanos.API.Services
{
    public class JwtTokenService : IJwtTokenService
    {
        private readonly IConfiguration _configuration;

        public JwtTokenService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string GenerateToken(User user)
        {
            var secretKey = _configuration["Jwt:Key"]
                ?? throw new InvalidOperationException("Jwt:Key não configurada.");

            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(JwtRegisteredClaimNames.Sub, user.Email),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new(ClaimTypes.Role, user.Role)
            };

            if (user.DriverId.HasValue)
            {
                claims.Add(new Claim("driverId", user.DriverId.Value.ToString()));
            }

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(2),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test Vanos.API.Tests --filter JwtTokenServiceTests`
Expected: PASS (3 tests)

- [ ] **Step 6: Commit**

```bash
git add Vanos.API/Services Vanos.API/Models/Roles.cs Vanos.API.Tests/JwtTokenServiceTests.cs
git commit -m "feat: add JwtTokenService issuing real role and driver id claims"
```

---

### Task 4: `AuthController.Register`

**Files:**
- Create: `Vanos.API/DTOs/RegisterRequest.cs`, `Vanos.API/DTOs/AuthResponse.cs`
- Modify: `Vanos.API/Controllers/AuthController.cs` (replace entire file)
- Test: `Vanos.API.Tests/AuthControllerTests.cs`
- Modify: `Vanos.API.Tests/TestHelpers.cs`

**Interfaces:**
- Consumes: `IPasswordHasher` (Task 2), `IJwtTokenService` (Task 3), `Roles` (Task 3).
- Produces: `AuthResponse { Token, Role }`, consumed by Task 5's `Login`. `TestHelpers.BuildParentPrincipal(int userId)` and `TestHelpers.BuildDriverPrincipal(int driverId)`, consumed by every controller test class from Task 7 onward.

- [ ] **Step 1: Extend `TestHelpers.cs` with principal builders**

```csharp
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Vanos.API.Data;
using Vanos.API.Models;

namespace Vanos.API.Tests
{
    public static class TestHelpers
    {
        public static AppDbContext BuildContext() =>
            new(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        public static ClaimsPrincipal BuildParentPrincipal(int userId) =>
            new(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, Roles.Parent)
            }, "TestAuth"));

        public static ClaimsPrincipal BuildDriverPrincipal(int driverId, int userId = 0) =>
            new(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, Roles.Driver),
                new Claim("driverId", driverId.ToString())
            }, "TestAuth"));
    }
}
```

- [ ] **Step 2: Write `RegisterRequest.cs` and `AuthResponse.cs`**

```csharp
namespace Vanos.API.DTOs
{
    public class RegisterRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;

        public string? Fullname { get; set; }
        public string? CPF { get; set; }
        public string? PhoneNumber { get; set; }
        public string? LicensePlate { get; set; }
        public int? StudentCapacity { get; set; }
        public string? PixKey { get; set; }
    }
}
```

```csharp
namespace Vanos.API.DTOs
{
    public class AuthResponse
    {
        public string Token { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }
}
```

- [ ] **Step 3: Write the failing tests**

```csharp
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
```

- [ ] **Step 4: Run the tests to verify they fail**

Run: `dotnet test Vanos.API.Tests --filter AuthControllerTests`
Expected: FAIL (compile error — `AuthController` constructor doesn't match yet)

- [ ] **Step 5: Rewrite `AuthController.cs`**

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vanos.API.Data;
using Vanos.API.DTOs;
using Vanos.API.Models;
using Vanos.API.Services;

namespace Vanos.API.Controllers
{
    [Route("api/auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IJwtTokenService _jwtTokenService;

        public AuthController(AppDbContext context, IPasswordHasher passwordHasher, IJwtTokenService jwtTokenService)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _jwtTokenService = jwtTokenService;
        }

        [HttpPost("register")]
        public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
        {
            if (request.Role != Roles.Driver && request.Role != Roles.Parent)
            {
                return BadRequest("Role deve ser 'Driver' ou 'Parent'.");
            }

            if (await _context.Users.AnyAsync(u => u.Email == request.Email))
            {
                return BadRequest("Este e-mail já está cadastrado.");
            }

            var user = new User
            {
                Email = request.Email,
                PasswordHash = _passwordHasher.Hash(request.Password),
                Role = request.Role
            };

            if (request.Role == Roles.Driver)
            {
                if (string.IsNullOrWhiteSpace(request.Fullname) ||
                    string.IsNullOrWhiteSpace(request.CPF) ||
                    string.IsNullOrWhiteSpace(request.PhoneNumber) ||
                    string.IsNullOrWhiteSpace(request.LicensePlate) ||
                    request.StudentCapacity is null ||
                    string.IsNullOrWhiteSpace(request.PixKey))
                {
                    return BadRequest("Dados do motorista incompletos.");
                }

                var driver = new Driver
                {
                    Fullname = request.Fullname,
                    CPF = request.CPF,
                    PhoneNumber = request.PhoneNumber,
                    LicensePlate = request.LicensePlate,
                    StudentCapacity = request.StudentCapacity.Value,
                    PixKey = request.PixKey
                };

                _context.Drivers.Add(driver);
                await _context.SaveChangesAsync();

                user.DriverId = driver.Id;
            }

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var token = _jwtTokenService.GenerateToken(user);
            return Ok(new AuthResponse { Token = token, Role = user.Role });
        }
    }
}
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test Vanos.API.Tests --filter AuthControllerTests`
Expected: PASS (4 tests)

- [ ] **Step 7: Commit**

```bash
git add Vanos.API/Controllers/AuthController.cs Vanos.API/DTOs/RegisterRequest.cs Vanos.API/DTOs/AuthResponse.cs Vanos.API.Tests/AuthControllerTests.cs Vanos.API.Tests/TestHelpers.cs
git commit -m "feat: add AuthController.Register for Driver and Parent roles"
```

---

### Task 5: `AuthController.Login` (fix password/role bug)

**Files:**
- Create: `Vanos.API/DTOs/LoginRequest.cs`
- Modify: `Vanos.API/Controllers/AuthController.cs`
- Modify: `Vanos.API.Tests/AuthControllerTests.cs`

**Interfaces:**
- Produces: `POST /api/auth/login` returning `AuthResponse`, replacing the old stub that ignored the password and hardcoded role `"Driver"`.

- [ ] **Step 1: Write `LoginRequest.cs`**

```csharp
namespace Vanos.API.DTOs
{
    public class LoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
```

- [ ] **Step 2: Add the failing tests to `AuthControllerTests.cs`**

```csharp
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
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test Vanos.API.Tests --filter AuthControllerTests`
Expected: FAIL (compile error — `Login` method doesn't exist on the new controller)

- [ ] **Step 4: Add `Login` to `AuthController.cs`** (append inside the class, after `Register`)

```csharp
        [HttpPost("login")]
        public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
        {
            var user = await _context.Users.SingleOrDefaultAsync(u => u.Email == request.Email);

            if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
            {
                return Unauthorized("E-mail ou senha inválidos.");
            }

            var token = _jwtTokenService.GenerateToken(user);
            return Ok(new AuthResponse { Token = token, Role = user.Role });
        }
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test Vanos.API.Tests --filter AuthControllerTests`
Expected: PASS (7 tests)

- [ ] **Step 6: Commit**

```bash
git add Vanos.API/Controllers/AuthController.cs Vanos.API/DTOs/LoginRequest.cs Vanos.API.Tests/AuthControllerTests.cs
git commit -m "fix: verify password and issue real role on login"
```

---

### Task 6: `Student`/`Driver` schema changes (nullable `DriverId`, `ParentId`, driver location)

**Files:**
- Modify: `Vanos.API/Models/Student.cs`
- Modify: `Vanos.API/Models/Driver.cs`
- Modify: `Vanos.API/Controllers/StudentsController.cs` (guard, fully rewritten in Task 11)
- Test: `Vanos.API.Tests/StudentModelTests.cs`
- Create: `Vanos.API/Migrations/<timestamp>_AddParentIdAndDriverLocation.cs` (generated)

**Interfaces:**
- Produces: `Student.DriverId : int?`, `Student.ParentId : int`, `Driver.Latitude : double?`, `Driver.Longitude : double?` — consumed by every task from here on.

- [ ] **Step 1: Write the failing test**

```csharp
using Vanos.API.Models;

namespace Vanos.API.Tests
{
    public class StudentModelTests
    {
        [Fact]
        public async Task Student_CanBeSavedWithoutAnAssignedDriver()
        {
            using var context = TestHelpers.BuildContext();

            context.Students.Add(new Student { FullName = "Lucas Silva", ParentId = 1, SchoolId = 1, DriverId = null });
            await context.SaveChangesAsync();

            var saved = Assert.Single(context.Students);
            Assert.Null(saved.DriverId);
            Assert.Equal(1, saved.ParentId);
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test Vanos.API.Tests --filter StudentModelTests`
Expected: FAIL (compile error — `Student.DriverId` isn't nullable, `ParentId` doesn't exist)

- [ ] **Step 3: Modify `Student.cs`**

Change:
```csharp
        public int SchoolId { get; set; }
        public int DriverId { get; set; }
```
to:
```csharp
        public int SchoolId { get; set; }
        public int ParentId { get; set; }
        public int? DriverId { get; set; }
```

- [ ] **Step 4: Modify `Driver.cs`**

Add after `IsActive`:
```csharp
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
```

- [ ] **Step 5: Guard the driver-existence check in `StudentsController.PostStudent`**

Change:
```csharp
            var driverExists = await _context.Drivers.AnyAsync(d => d.Id == student.DriverId);
            var schoolExists = await _context.Schools.AnyAsync(s => s.Id == student.SchoolId);

            if (!driverExists)
            {
                return BadRequest("Motorista não encontrado. Verifique o DriverId repassado.");
            }

            if (!schoolExists)
```
to:
```csharp
            if (student.DriverId.HasValue && !await _context.Drivers.AnyAsync(d => d.Id == student.DriverId.Value))
            {
                return BadRequest("Motorista não encontrado. Verifique o DriverId repassado.");
            }

            var schoolExists = await _context.Schools.AnyAsync(s => s.Id == student.SchoolId);

            if (!schoolExists)
```

- [ ] **Step 6: Run the test to verify it passes**

Run: `dotnet test Vanos.API.Tests --filter StudentModelTests`
Expected: PASS (1 test)

- [ ] **Step 7: Confirm the whole suite still builds and passes**

Run: `dotnet test Vanos.API.Tests`
Expected: PASS (all tests so far)

- [ ] **Step 8: Generate the EF Core migration**

Run from `Vanos.API/`:
```bash
dotnet ef migrations add AddParentIdAndDriverLocation
```
Expected: a new file under `Migrations/` adding `ParentId` (non-nullable int) and `Latitude`/`Longitude` columns, and altering `Student.DriverId` to be nullable. This is a dev-only database with no production data, so no manual default-value backfill is required.

- [ ] **Step 9: Commit**

```bash
git add Vanos.API/Models/Student.cs Vanos.API/Models/Driver.cs Vanos.API/Controllers/StudentsController.cs Vanos.API/Migrations Vanos.API.Tests/StudentModelTests.cs
git commit -m "feat: make Student.DriverId optional, add ParentId and Driver location"
```

---

### Task 7: `DriverSchool` + `DriversController.UpdateSchoolsServed`

**Files:**
- Create: `Vanos.API/Models/DriverSchool.cs`
- Modify: `Vanos.API/Data/AppDbContext.cs`
- Modify: `Vanos.API/Controllers/DriversController.cs`
- Test: `Vanos.API.Tests/DriversControllerTests.cs`
- Create: `Vanos.API/Migrations/<timestamp>_AddDriverSchool.cs` (generated)

**Interfaces:**
- Consumes: `ClaimsPrincipal` extension `GetDriverId()` — written in this task in `Vanos.API/Extensions/ClaimsPrincipalExtensions.cs` since this is its first consumer.
- Produces: `DriverSchool { DriverId, SchoolId }`, `PUT /api/drivers/{id}/schools`.

- [ ] **Step 1: Write `DriverSchool.cs`**

```csharp
namespace Vanos.API.Models
{
    public class DriverSchool
    {
        public int DriverId { get; set; }
        public int SchoolId { get; set; }
    }
}
```

- [ ] **Step 2: Write `ClaimsPrincipalExtensions.cs`**

```csharp
using System.Security.Claims;

namespace Vanos.API.Extensions
{
    public static class ClaimsPrincipalExtensions
    {
        public static int GetUserId(this ClaimsPrincipal user)
        {
            var value = user.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? throw new InvalidOperationException("Usuário sem identificador no token.");

            return int.Parse(value);
        }

        public static int GetDriverId(this ClaimsPrincipal user)
        {
            var value = user.FindFirstValue("driverId")
                ?? throw new InvalidOperationException("Usuário autenticado não é um motorista.");

            return int.Parse(value);
        }
    }
}
```

- [ ] **Step 3: Add `DriverSchools` to `AppDbContext.cs` with its composite key**

```csharp
        public DbSet<Driver> Drivers { get; set; }
        public DbSet<Student> Students { get; set; }
        public DbSet<School> Schools { get; set; }
        public DbSet<MonthlyFee> MonthlyFees { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<DriverSchool> DriverSchools { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DriverSchool>().HasKey(ds => new { ds.DriverId, ds.SchoolId });
        }
```

- [ ] **Step 4: Write the failing tests**

```csharp
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
```

- [ ] **Step 5: Run the tests to verify they fail**

Run: `dotnet test Vanos.API.Tests --filter DriversControllerTests`
Expected: FAIL (compile error — `UpdateSchoolsServed` doesn't exist yet)

- [ ] **Step 6: Add `UpdateSchoolsServed` to `DriversController.cs`**

Add these usings at the top:
```csharp
using Microsoft.AspNetCore.Authorization;
using Vanos.API.Extensions;
using Vanos.API.Models;
```

Add inside the class, after `GetStudentsByDriver`:
```csharp
        [HttpPut("{id}/schools")]
        [Authorize(Roles = Roles.Driver)]
        public async Task<IActionResult> UpdateSchoolsServed(int id, [FromBody] List<int> schoolIds)
        {
            if (id != User.GetDriverId())
            {
                return Forbid();
            }

            var driverExists = await _context.Drivers.AnyAsync(d => d.Id == id);
            if (!driverExists)
            {
                return NotFound("Motorista não encontrado.");
            }

            var existing = _context.DriverSchools.Where(ds => ds.DriverId == id);
            _context.DriverSchools.RemoveRange(existing);

            foreach (var schoolId in schoolIds.Distinct())
            {
                _context.DriverSchools.Add(new DriverSchool { DriverId = id, SchoolId = schoolId });
            }

            await _context.SaveChangesAsync();
            return NoContent();
        }
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test Vanos.API.Tests --filter DriversControllerTests`
Expected: PASS (2 tests)

- [ ] **Step 8: Generate the EF Core migration**

Run from `Vanos.API/`:
```bash
dotnet ef migrations add AddDriverSchool
```
Expected: a new migration creating the `DriverSchools` table with a composite primary key on (`DriverId`, `SchoolId`).

- [ ] **Step 9: Commit**

```bash
git add Vanos.API/Models/DriverSchool.cs Vanos.API/Extensions Vanos.API/Data/AppDbContext.cs Vanos.API/Controllers/DriversController.cs Vanos.API/Migrations Vanos.API.Tests/DriversControllerTests.cs
git commit -m "feat: let drivers declare which schools they serve"
```

---

### Task 8: `HireRequest` model + `HireRequestsController.Create`

**Files:**
- Create: `Vanos.API/Models/HireRequest.cs`
- Create: `Vanos.API/DTOs/HireRequestCreateRequest.cs`
- Create: `Vanos.API/Controllers/HireRequestsController.cs`
- Modify: `Vanos.API/Data/AppDbContext.cs`
- Test: `Vanos.API.Tests/HireRequestsControllerTests.cs`
- Create: `Vanos.API/Migrations/<timestamp>_AddHireRequest.cs` (generated)

**Interfaces:**
- Produces: `HireRequest { Id, StudentId, DriverId, Status (HireRequestStatus), CreatedAt, RespondedAt }`, `HireRequestStatus { Pending, Accepted, Rejected }` — consumed by Task 9, 10, 12, 13.

- [ ] **Step 1: Write `HireRequest.cs`**

```csharp
namespace Vanos.API.Models
{
    public enum HireRequestStatus
    {
        Pending,
        Accepted,
        Rejected
    }

    public class HireRequest
    {
        public int Id { get; set; }
        public int StudentId { get; set; }
        public int DriverId { get; set; }
        public HireRequestStatus Status { get; set; } = HireRequestStatus.Pending;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? RespondedAt { get; set; }
    }
}
```

- [ ] **Step 2: Write `HireRequestCreateRequest.cs`**

```csharp
namespace Vanos.API.DTOs
{
    public class HireRequestCreateRequest
    {
        public int StudentId { get; set; }
        public int DriverId { get; set; }
    }
}
```

- [ ] **Step 3: Add `HireRequests` to `AppDbContext.cs`**

```csharp
        public DbSet<DriverSchool> DriverSchools { get; set; }
        public DbSet<HireRequest> HireRequests { get; set; }
```

- [ ] **Step 4: Write the failing tests**

```csharp
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
```

- [ ] **Step 5: Run the tests to verify they fail**

Run: `dotnet test Vanos.API.Tests --filter HireRequestsControllerTests`
Expected: FAIL (compile error — `HireRequestsController` doesn't exist yet)

- [ ] **Step 6: Write `HireRequestsController.cs`**

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vanos.API.Data;
using Vanos.API.DTOs;
using Vanos.API.Extensions;
using Vanos.API.Models;

namespace Vanos.API.Controllers
{
    [Route("api/hirerequests")]
    [ApiController]
    [Authorize]
    public class HireRequestsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public HireRequestsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        [Authorize(Roles = Roles.Parent)]
        public async Task<ActionResult<HireRequest>> Create(HireRequestCreateRequest request)
        {
            var student = await _context.Students.FindAsync(request.StudentId);
            if (student is null)
            {
                return NotFound("Aluno não encontrado.");
            }

            if (student.ParentId != User.GetUserId())
            {
                return Forbid();
            }

            if (student.DriverId.HasValue)
            {
                return BadRequest("Este aluno já possui um motorista.");
            }

            var hasPendingRequest = await _context.HireRequests
                .AnyAsync(r => r.StudentId == request.StudentId && r.Status == HireRequestStatus.Pending);

            if (hasPendingRequest)
            {
                return BadRequest("Este aluno já possui uma solicitação pendente.");
            }

            var driverExists = await _context.Drivers.AnyAsync(d => d.Id == request.DriverId);
            if (!driverExists)
            {
                return BadRequest("Motorista não encontrado.");
            }

            var hireRequest = new HireRequest
            {
                StudentId = request.StudentId,
                DriverId = request.DriverId
            };

            _context.HireRequests.Add(hireRequest);
            await _context.SaveChangesAsync();

            return Ok(hireRequest);
        }
    }
}
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test Vanos.API.Tests --filter HireRequestsControllerTests`
Expected: PASS (4 tests)

- [ ] **Step 8: Generate the EF Core migration**

Run from `Vanos.API/`:
```bash
dotnet ef migrations add AddHireRequest
```
Expected: a new migration creating the `HireRequests` table, with `Status` stored as an int.

- [ ] **Step 9: Commit**

```bash
git add Vanos.API/Models/HireRequest.cs Vanos.API/DTOs/HireRequestCreateRequest.cs Vanos.API/Controllers/HireRequestsController.cs Vanos.API/Data/AppDbContext.cs Vanos.API/Migrations Vanos.API.Tests/HireRequestsControllerTests.cs
git commit -m "feat: let parents send hire requests to drivers"
```

---

### Task 9: `HireRequestsController.Accept` / `.Reject`

**Files:**
- Modify: `Vanos.API/Controllers/HireRequestsController.cs`
- Modify: `Vanos.API.Tests/HireRequestsControllerTests.cs`

**Interfaces:**
- Produces: `PATCH /api/hirerequests/{id}/accept`, `PATCH /api/hirerequests/{id}/reject`.

- [ ] **Step 1: Add the failing tests to `HireRequestsControllerTests.cs`**

```csharp
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
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test Vanos.API.Tests --filter HireRequestsControllerTests`
Expected: FAIL (compile error — `Accept`/`Reject` don't exist yet)

- [ ] **Step 3: Add `Accept` and `Reject` to `HireRequestsController.cs`** (append inside the class, after `Create`)

```csharp
        [HttpPatch("{id}/accept")]
        [Authorize(Roles = Roles.Driver)]
        public async Task<IActionResult> Accept(int id)
        {
            var hireRequest = await _context.HireRequests.FindAsync(id);
            if (hireRequest is null)
            {
                return NotFound("Solicitação não encontrada.");
            }

            if (hireRequest.DriverId != User.GetDriverId())
            {
                return Forbid();
            }

            if (hireRequest.Status != HireRequestStatus.Pending)
            {
                return BadRequest("Esta solicitação já foi respondida.");
            }

            hireRequest.Status = HireRequestStatus.Accepted;
            hireRequest.RespondedAt = DateTime.UtcNow;

            var student = await _context.Students.FindAsync(hireRequest.StudentId);
            if (student is not null)
            {
                student.DriverId = hireRequest.DriverId;
            }

            var otherPendingRequests = await _context.HireRequests
                .Where(r => r.StudentId == hireRequest.StudentId && r.Id != hireRequest.Id && r.Status == HireRequestStatus.Pending)
                .ToListAsync();

            foreach (var other in otherPendingRequests)
            {
                other.Status = HireRequestStatus.Rejected;
                other.RespondedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return Ok(hireRequest);
        }

        [HttpPatch("{id}/reject")]
        [Authorize(Roles = Roles.Driver)]
        public async Task<IActionResult> Reject(int id)
        {
            var hireRequest = await _context.HireRequests.FindAsync(id);
            if (hireRequest is null)
            {
                return NotFound("Solicitação não encontrada.");
            }

            if (hireRequest.DriverId != User.GetDriverId())
            {
                return Forbid();
            }

            if (hireRequest.Status != HireRequestStatus.Pending)
            {
                return BadRequest("Esta solicitação já foi respondida.");
            }

            hireRequest.Status = HireRequestStatus.Rejected;
            hireRequest.RespondedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(hireRequest);
        }
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test Vanos.API.Tests --filter HireRequestsControllerTests`
Expected: PASS (8 tests)

- [ ] **Step 5: Commit**

```bash
git add Vanos.API/Controllers/HireRequestsController.cs Vanos.API.Tests/HireRequestsControllerTests.cs
git commit -m "feat: let drivers accept or reject hire requests"
```

---

### Task 10: `HireRequestsController.GetHireRequests` (list)

**Files:**
- Modify: `Vanos.API/Controllers/HireRequestsController.cs`
- Modify: `Vanos.API.Tests/HireRequestsControllerTests.cs`

**Interfaces:**
- Produces: `GET /api/hirerequests?driverId=` and `GET /api/hirerequests?parentId=`.

- [ ] **Step 1: Add the failing tests to `HireRequestsControllerTests.cs`**

```csharp
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
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test Vanos.API.Tests --filter HireRequestsControllerTests`
Expected: FAIL (compile error — `GetHireRequests` doesn't exist yet)

- [ ] **Step 3: Add `GetHireRequests` to `HireRequestsController.cs`** (append inside the class)

```csharp
        [HttpGet]
        public async Task<ActionResult<IEnumerable<HireRequest>>> GetHireRequests([FromQuery] int? driverId, [FromQuery] int? parentId)
        {
            if (driverId is not null)
            {
                if (driverId != User.GetDriverId())
                {
                    return Forbid();
                }

                return await _context.HireRequests.Where(r => r.DriverId == driverId).ToListAsync();
            }

            if (parentId is not null)
            {
                if (parentId != User.GetUserId())
                {
                    return Forbid();
                }

                var studentIds = await _context.Students
                    .Where(s => s.ParentId == parentId)
                    .Select(s => s.Id)
                    .ToListAsync();

                return await _context.HireRequests.Where(r => studentIds.Contains(r.StudentId)).ToListAsync();
            }

            return BadRequest("Informe driverId ou parentId.");
        }
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test Vanos.API.Tests --filter HireRequestsControllerTests`
Expected: PASS (11 tests)

- [ ] **Step 5: Commit**

```bash
git add Vanos.API/Controllers/HireRequestsController.cs Vanos.API.Tests/HireRequestsControllerTests.cs
git commit -m "feat: list hire requests by driver or parent"
```

---

### Task 11: `StudentsController.PostStudent` requires `ParentId` from JWT

**Files:**
- Modify: `Vanos.API/Controllers/StudentsController.cs`
- Test: `Vanos.API.Tests/StudentsControllerTests.cs`

**Interfaces:**
- Produces: `POST /api/students` now sets `ParentId` from the JWT and ignores any client-supplied `DriverId`.

- [ ] **Step 1: Write the failing tests**

```csharp
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
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test Vanos.API.Tests --filter StudentsControllerTests`
Expected: FAIL — `PostStudent_SetsParentIdFromJwtAndLeavesDriverUnassigned` fails because `DriverId = 999` is still accepted as-is and `ParentId` isn't set from the JWT

- [ ] **Step 3: Rewrite `PostStudent` in `StudentsController.cs`**

Add these usings at the top:
```csharp
using Microsoft.AspNetCore.Authorization;
using Vanos.API.Extensions;
using Vanos.API.Models;
```

Replace the whole method body:
```csharp
        [HttpPost]
        [Authorize(Roles = Roles.Parent)]
        public async Task<ActionResult<Student>> PostStudent(Student student)
        {
            var schoolExists = await _context.Schools.AnyAsync(s => s.Id == student.SchoolId);
            if (!schoolExists)
            {
                return BadRequest("Escola não encontrada. Verifique o SchoolId repassado.");
            }

            student.ParentId = User.GetUserId();
            student.DriverId = null;

            _context.Students.Add(student);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetStudents), new { id = student.Id }, student);
        }
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test Vanos.API.Tests --filter StudentsControllerTests`
Expected: PASS (2 tests)

- [ ] **Step 5: Run the full suite to confirm nothing else broke**

Run: `dotnet test Vanos.API.Tests`
Expected: PASS (all tests)

- [ ] **Step 6: Commit**

```bash
git add Vanos.API/Controllers/StudentsController.cs Vanos.API.Tests/StudentsControllerTests.cs
git commit -m "feat: require parent-owned student creation, driver assigned only via hire request"
```

---

### Task 12: `Rating` model + `RatingsController`

**Files:**
- Create: `Vanos.API/Models/Rating.cs`
- Create: `Vanos.API/DTOs/RatingCreateRequest.cs`
- Create: `Vanos.API/Controllers/RatingsController.cs`
- Modify: `Vanos.API/Data/AppDbContext.cs`
- Test: `Vanos.API.Tests/RatingsControllerTests.cs`
- Create: `Vanos.API/Migrations/<timestamp>_AddRating.cs` (generated)

**Interfaces:**
- Produces: `Rating { Id, DriverId, ParentId, Score, Comment, CreatedAt }`, `POST /api/ratings`, `GET /api/drivers/{driverId}/ratings` — the ratings-average shape (`Average`, `Count`, `Ratings`) is consumed by Task 13's search endpoint.

- [ ] **Step 1: Write `Rating.cs`**

```csharp
namespace Vanos.API.Models
{
    public class Rating
    {
        public int Id { get; set; }
        public int DriverId { get; set; }
        public int ParentId { get; set; }
        public int Score { get; set; }
        public string? Comment { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
```

- [ ] **Step 2: Write `RatingCreateRequest.cs`**

```csharp
namespace Vanos.API.DTOs
{
    public class RatingCreateRequest
    {
        public int DriverId { get; set; }
        public int Score { get; set; }
        public string? Comment { get; set; }
    }
}
```

- [ ] **Step 3: Add `Ratings` to `AppDbContext.cs` with its unique index**

```csharp
        public DbSet<HireRequest> HireRequests { get; set; }
        public DbSet<Rating> Ratings { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DriverSchool>().HasKey(ds => new { ds.DriverId, ds.SchoolId });
            modelBuilder.Entity<Rating>().HasIndex(r => new { r.DriverId, r.ParentId }).IsUnique();
        }
```

- [ ] **Step 4: Write the failing tests**

```csharp
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
```

- [ ] **Step 5: Run the tests to verify they fail**

Run: `dotnet test Vanos.API.Tests --filter RatingsControllerTests`
Expected: FAIL (compile error — `RatingsController` doesn't exist yet)

- [ ] **Step 6: Write `RatingsController.cs`**

```csharp
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
        [Authorize(Roles = Roles.Parent)]
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
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test Vanos.API.Tests --filter RatingsControllerTests`
Expected: PASS (5 tests)

- [ ] **Step 8: Generate the EF Core migration**

Run from `Vanos.API/`:
```bash
dotnet ef migrations add AddRating
```
Expected: a new migration creating the `Ratings` table with a unique index on (`DriverId`, `ParentId`).

- [ ] **Step 9: Commit**

```bash
git add Vanos.API/Models/Rating.cs Vanos.API/DTOs/RatingCreateRequest.cs Vanos.API/Controllers/RatingsController.cs Vanos.API/Data/AppDbContext.cs Vanos.API/Migrations Vanos.API.Tests/RatingsControllerTests.cs
git commit -m "feat: let parents rate drivers after being hired"
```

---

### Task 13: `DriversController.Search`

**Files:**
- Modify: `Vanos.API/Controllers/DriversController.cs`
- Modify: `Vanos.API.Tests/DriversControllerTests.cs`

**Interfaces:**
- Produces: `GET /api/drivers/search?schoolId=&lat=&lng=&radiusKm=`.

- [ ] **Step 1: Add the failing tests to `DriversControllerTests.cs`**

```csharp
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
        public async Task Search_ComputesAvailableCapacityFromAcceptedHireRequestsOnly()
        {
            using var context = TestHelpers.BuildContext();
            context.Drivers.Add(new Driver { Id = 1, Fullname = "Cauan", CPF = "1", PhoneNumber = "1", LicensePlate = "X", StudentCapacity = 10, PixKey = "x", IsActive = true });
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
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test Vanos.API.Tests --filter DriversControllerTests`
Expected: FAIL (compile error — `Search` doesn't exist yet)

- [ ] **Step 3: Add `Search` and its private helpers to `DriversController.cs`** (append inside the class)

```csharp
        [HttpGet("search")]
        [Authorize(Roles = Roles.Parent)]
        public async Task<ActionResult<IEnumerable<object>>> Search([FromQuery] int? schoolId, [FromQuery] double? lat, [FromQuery] double? lng, [FromQuery] double? radiusKm)
        {
            var driversQuery = _context.Drivers.Where(d => d.IsActive);

            if (schoolId is not null)
            {
                var driverIdsForSchool = _context.DriverSchools
                    .Where(ds => ds.SchoolId == schoolId)
                    .Select(ds => ds.DriverId);

                driversQuery = driversQuery.Where(d => driverIdsForSchool.Contains(d.Id));
            }

            var drivers = await driversQuery.ToListAsync();

            if (lat is not null && lng is not null && radiusKm is not null)
            {
                drivers = drivers
                    .Where(d => d.Latitude is not null && d.Longitude is not null &&
                                DistanceInKm(lat.Value, lng.Value, d.Latitude.Value, d.Longitude.Value) <= radiusKm.Value)
                    .ToList();
            }

            var results = new List<object>();
            foreach (var driver in drivers)
            {
                var acceptedStudentCount = await _context.HireRequests
                    .Where(r => r.DriverId == driver.Id && r.Status == HireRequestStatus.Accepted)
                    .CountAsync();

                var ratings = await _context.Ratings.Where(r => r.DriverId == driver.Id).ToListAsync();

                results.Add(new
                {
                    driver.Id,
                    driver.Fullname,
                    driver.PhoneNumber,
                    driver.StudentCapacity,
                    AvailableCapacity = driver.StudentCapacity - acceptedStudentCount,
                    AverageRating = ratings.Count == 0 ? (double?)null : ratings.Average(r => r.Score)
                });
            }

            return Ok(results);
        }

        private static double DistanceInKm(double lat1, double lon1, double lat2, double lon2)
        {
            const double earthRadiusKm = 6371;
            var dLat = DegreesToRadians(lat2 - lat1);
            var dLon = DegreesToRadians(lon2 - lon1);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return earthRadiusKm * c;
        }

        private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180;
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test Vanos.API.Tests --filter DriversControllerTests`
Expected: PASS (5 tests)

- [ ] **Step 5: Commit**

```bash
git add Vanos.API/Controllers/DriversController.cs Vanos.API.Tests/DriversControllerTests.cs
git commit -m "feat: add driver search by school and location"
```

---

### Task 14: Wire DI/authorization in `Program.cs`, extend `Vanos.API.http`, final full-suite run

**Files:**
- Modify: `Vanos.API/Program.cs`
- Modify: `Vanos.API/Vanos.API.http`

**Interfaces:**
- Produces: the running application with `IPasswordHasher`/`IJwtTokenService` registered in DI and `AddAuthorization()`/`UseAuthorization()` wired, so `[Authorize]` attributes added in Tasks 4–13 actually take effect at runtime (unit tests bypass the ASP.NET pipeline entirely, so this is the only task that exercises that wiring).

- [ ] **Step 1: Modify `Program.cs`**

Add these usings at the top:
```csharp
using Vanos.API.Services;
```

Add after `builder.Services.AddDbContext<AppDbContext>(...)`:
```csharp
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddAuthorization();
```

Add `app.UseAuthorization();` after `app.UseAuthentication();`:
```csharp
app.UseAuthentication();
app.UseAuthorization();
```

- [ ] **Step 2: Confirm the app builds and starts**

Run: `dotnet build Vanos.API`
Expected: Build succeeded, 0 errors

- [ ] **Step 3: Extend `Vanos.API.http` with the full matching flow**

Append to the end of the file:
```http
### Registrar um Pai/Responsável
POST {{Vanos.API_HostAddress}}/api/auth/register
Content-Type: application/json

{
  "email": "ana.silva@example.com",
  "password": "Sup3rSecret!",
  "role": "Parent"
}

### Registrar um Motorista
POST {{Vanos.API_HostAddress}}/api/auth/register
Content-Type: application/json

{
  "email": "cauan.braga@example.com",
  "password": "Sup3rSecret!",
  "role": "Driver",
  "fullname": "Cauan Braga",
  "cpf": "123.456.789-00",
  "phoneNumber": "19 98888-7777",
  "licensePlate": "VAN-2026",
  "studentCapacity": 15,
  "pixKey": "cauan@email.com"
}

### Login como Motorista (copiar o token retornado para as próximas requisições)
POST {{Vanos.API_HostAddress}}/api/auth/login
Content-Type: application/json

{
  "email": "cauan.braga@example.com",
  "password": "Sup3rSecret!"
}

### Motorista declara as escolas que atende
PUT {{Vanos.API_HostAddress}}/api/drivers/1/schools
Content-Type: application/json
Authorization: Bearer {{driverToken}}

[1, 2]

### Login como Pai/Responsável (copiar o token retornado para as próximas requisições)
POST {{Vanos.API_HostAddress}}/api/auth/login
Content-Type: application/json

{
  "email": "ana.silva@example.com",
  "password": "Sup3rSecret!"
}

### Pai cadastra um aluno (sem motorista ainda)
POST {{Vanos.API_HostAddress}}/api/students
Content-Type: application/json
Authorization: Bearer {{parentToken}}

{
  "fullName": "Lucas Silva",
  "guardianName": "Ana Silva",
  "guardianPhoneNumber": "19 97777-6666",
  "zipCode": "13086-900",
  "street": "Rodovia Dom Pedro I",
  "number": "Km 136",
  "neighborhood": "Parque das Universidades",
  "city": "Campinas",
  "state": "SP",
  "schoolId": 1,
  "isGoingToday": true,
  "isReturningToday": true
}

### Pai busca motoristas pela escola
GET {{Vanos.API_HostAddress}}/api/drivers/search?schoolId=1
Authorization: Bearer {{parentToken}}

### Pai envia uma solicitação de contratação
POST {{Vanos.API_HostAddress}}/api/hirerequests
Content-Type: application/json
Authorization: Bearer {{parentToken}}

{
  "studentId": 1,
  "driverId": 1
}

### Motorista vê solicitações recebidas
GET {{Vanos.API_HostAddress}}/api/hirerequests?driverId=1
Authorization: Bearer {{driverToken}}

### Motorista aceita a solicitação
PATCH {{Vanos.API_HostAddress}}/api/hirerequests/1/accept
Authorization: Bearer {{driverToken}}

### Pai avalia o motorista
POST {{Vanos.API_HostAddress}}/api/ratings
Content-Type: application/json
Authorization: Bearer {{parentToken}}

{
  "driverId": 1,
  "score": 5,
  "comment": "Pontual e cuidadoso."
}

### Ver avaliações do motorista
GET {{Vanos.API_HostAddress}}/api/drivers/1/ratings
```

- [ ] **Step 4: Run the full automated test suite**

Run: `dotnet test Vanos.API.Tests`
Expected: PASS (all tests across every task in this plan)

- [ ] **Step 5: Commit**

```bash
git add Vanos.API/Program.cs Vanos.API/Vanos.API.http
git commit -m "feat: wire auth/DI for matching feature and document full flow in .http file"
```

- [ ] **Step 6: Push to origin**

```bash
git push origin master
```
