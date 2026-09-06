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
