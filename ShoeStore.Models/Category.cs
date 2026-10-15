using System.ComponentModel.DataAnnotations;

namespace ShoeStore.Models;

public class Category { public int Id { get; set; } [Required, StringLength(60)] public string Name { get; set; } = ""; public List<Product> Products { get; set; } = []; }
