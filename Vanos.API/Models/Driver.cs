namespace Vanos.API.Models
{
    public class Driver
    {
        public int Id { get; set; }
        public string Fullname { get; set; } = string.Empty;
        public string CPF { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string LicensePlate { get; set; } = string.Empty;
        public int StudentCapacity { get; set; }
        public string PixKey { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;

    }
}
