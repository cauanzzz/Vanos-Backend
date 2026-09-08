namespace Vanos.API.Models
{
    public class User
    {
        public int Id { get; set; }

        public string Email { get; set; } = string.Empty;

        public string PasswordHash { get; set; } = string.Empty;

        public string Role { get; set; } = string.Empty;

        public int? DriverId { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        public Driver? Driver { get; set; }
    }
}