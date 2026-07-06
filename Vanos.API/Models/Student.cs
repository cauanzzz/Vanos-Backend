namespace Vanos.API.Models
{
    public class Student
    {
        public int Id { get; set; }

        public string FullName { get; set; } = string.Empty;

        public string GuardianName { get; set; } = string.Empty;
        public string GuardianPhoneNumber { get; set; } = string.Empty;

        public string ZipCode { get; set; } = string.Empty; 
        public string Street { get; set; } = string.Empty;
        public string Number { get; set; } = string.Empty;
        public string Complement { get; set; } = string.Empty;
        public string Neighborhood { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;

        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        public int SchoolId { get; set; }
        public int DriverId { get; set; }

        public bool IsGoingToday { get; set; } = true;
        public bool IsReturningToday { get; set; } = true;
    }
}