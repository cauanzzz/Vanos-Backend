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
