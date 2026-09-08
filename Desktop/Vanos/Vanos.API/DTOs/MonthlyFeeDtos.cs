using System.ComponentModel.DataAnnotations;
namespace Vanos.API.DTOs;
public sealed class MonthlyFeeCreateRequest
{
    [Range(1, int.MaxValue)] public int StudentId { get; set; }
    [Range(typeof(decimal), "0.01", "999999.99")] public decimal Amount { get; set; }
    public DateTime DueDate { get; set; }
}
public sealed record MonthlyFeeResponse(int Id, int StudentId, int DriverId,
    decimal Amount, DateTime DueDate, bool IsPaid, DateTime? PaymentDate, bool IsSimulated);
