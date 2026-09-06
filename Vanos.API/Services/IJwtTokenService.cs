using Vanos.API.Models;

namespace Vanos.API.Services
{
    public interface IJwtTokenService
    {
        string GenerateToken(User user);
    }
}
