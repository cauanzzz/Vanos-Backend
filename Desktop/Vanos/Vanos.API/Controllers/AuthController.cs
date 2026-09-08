using Microsoft.AspNetCore.Authorization;
using System.ComponentModel.DataAnnotations;
using Microsoft.Data.SqlClient;
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
    [AllowAnonymous]
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
            if (request.Role != Roles.Driver && request.Role != Roles.Parent && request.Role != Roles.Student)
            {
                return BadRequest("Role deve ser 'Driver', 'Parent' ou 'Student'.");
            }

            if (string.IsNullOrWhiteSpace(request.Email) || request.Email.Trim().Length > 254 ||
                !new EmailAddressAttribute().IsValid(request.Email.Trim()) ||
                string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8 ||
                System.Text.Encoding.UTF8.GetByteCount(request.Password) > 72)
                return BadRequest("E-mail inválido ou senha fora do limite (8 caracteres a 72 bytes UTF-8).");
            request.Email = request.Email.Trim().ToLowerInvariant();
            if (await _context.Users.AnyAsync(u => u.Email.ToLower() == request.Email))
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
                    request.StudentCapacity is null or <= 0 or > 100 ||
                    string.IsNullOrWhiteSpace(request.PixKey) ||
                    (request.Latitude.HasValue != request.Longitude.HasValue) ||
                    (request.Latitude.HasValue && (!double.IsFinite(request.Latitude.Value) || request.Latitude.Value is < -90 or > 90)) ||
                    (request.Longitude.HasValue && (!double.IsFinite(request.Longitude.Value) || request.Longitude.Value is < -180 or > 180)))
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
                    PixKey = request.PixKey,
                    Latitude = request.Latitude,
                    Longitude = request.Longitude
                };

                user.Driver = driver;
            }

            _context.Users.Add(user);
            try { await _context.SaveChangesAsync(); }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && (sql.Number == 2601 || sql.Number == 2627))
            { return Conflict("Este e-mail já está cadastrado."); }

            var token = _jwtTokenService.GenerateToken(user);
            return Ok(new AuthResponse { Token = token, Role = user.Role });
        }

        [HttpPost("login")]
        public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password) ||
                System.Text.Encoding.UTF8.GetByteCount(request.Password) > 72)
                return Unauthorized("E-mail ou senha inválidos.");
            var email = request.Email.Trim().ToLowerInvariant();
            var user = await _context.Users.SingleOrDefaultAsync(u => u.Email.ToLower() == email);

            if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
            {
                return Unauthorized("E-mail ou senha inválidos.");
            }

            var token = _jwtTokenService.GenerateToken(user);
            return Ok(new AuthResponse { Token = token, Role = user.Role });
        }
    }
}
