namespace Vanos.API.Models;
public sealed class DriverServiceArea
{
    public int DriverId { get; set; }
    public string City { get; set; } = string.Empty;
    public string Neighborhood { get; set; } = string.Empty;
}
