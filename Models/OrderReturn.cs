using System.ComponentModel.DataAnnotations;

namespace StyleMart.Models
{
    public class OrderReturn
    {

        public int Id { get; set; }

        [Required]
        public int OrderId { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        public string Reason { get; set; } = string.Empty;

        public string? Description { get; set; }

        public string Status { get; set; } = "Pending";

        public string RefundStatus { get; set; } = "Not Initiated";

        public DateTime RequestDate { get; set; } = DateTime.Now;
        public string RequestType { get; set; } = "Return";
    }


    



}
