using System;

namespace Vanos.API.Models
{
    public class MonthlyFee
    {
        public int Id { get; set; }

        public int StudentId { get; set; }
        public int DriverId { get; set; }

        public decimal Amount { get; set; }

        public DateTime DueDate { get; set; } 
        public bool IsPaid { get; set; } = false; 
        public DateTime? PaymentDate { get; set; } 
    }
}