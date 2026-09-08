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
