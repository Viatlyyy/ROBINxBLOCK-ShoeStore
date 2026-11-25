using System.ComponentModel.DataAnnotations;

namespace ShoeStore.Models;

public class Order
{
    public int Id { get; set; }
    public Guid PublicId { get; set; } = Guid.NewGuid();
    public string? UserId { get; set; }
    [Required, StringLength(80)]
    public string CustomerName { get; set; } = "";
    [StringLength(80)]
    public string? LastName { get; set; }
    [StringLength(254)]
    public string? Email { get; set; }
    [Required, StringLength(32)]
    public string Phone { get; set; } = "";
    [Required, StringLength(32)]
    public string DeliveryMethod { get; set; } = "Самовывоз";
    [StringLength(80)]
    public string? City { get; set; }
    [StringLength(120)]
    public string? Street { get; set; }
    [StringLength(20)]
    public string? House { get; set; }
    [StringLength(20)]
    public string? Apartment { get; set; }
    [StringLength(12)]
    public string? Postcode { get; set; }
    [StringLength(500)]
    public string? Comment { get; set; }
    [Required, StringLength(24)]
    public string PaymentMethod { get; set; } = "cash";
    [Required, StringLength(24)]
    public string Status { get; set; } = "Новый";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public decimal Total { get; set; }
    public List<OrderItem> Items { get; set; } =
    [];
}
