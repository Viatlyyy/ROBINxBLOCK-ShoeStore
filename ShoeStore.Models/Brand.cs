using System.ComponentModel.DataAnnotations;

namespace ShoeStore.Models;

public class Brand { public int Id { get; set; } [Required, StringLength(60)] public string Name { get; set; } = ""; public string? Description { get; set; } public List<Product> Products { get; set; } = []; }
