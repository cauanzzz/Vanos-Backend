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
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
    }
}
