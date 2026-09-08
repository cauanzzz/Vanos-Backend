using System.ComponentModel.DataAnnotations;
namespace Vanos.API.DTOs;
public sealed class ServiceAreaRequest
{
    [Required, StringLength(100)]
    public string City { get; set; } = string.Empty;
    [Required, StringLength(100)]
    public string Neighborhood { get; set; } = string.Empty;
}
public sealed record DriverListing(int Id, string Fullname, int StudentCapacity,
    int AvailableCapacity, double? AverageRating);
public sealed record PageResult<T>(int Page, int PageSize, bool HasNextPage, IReadOnlyList<T> Items);
