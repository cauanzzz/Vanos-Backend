namespace Vanos.API.DTOs
{
    public class RatingCreateRequest
    {
        public int DriverId { get; set; }
        public int Score { get; set; }
        public string? Comment { get; set; }
    }
}
