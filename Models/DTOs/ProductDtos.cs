namespace FreshGroupSystem.Models.DTOs;

/// <summary>
/// 产品返回结果
/// </summary>
public class ProductDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? Unit { get; set; }
    public decimal Price { get; set; }
    public string? ImageUrl { get; set; }
    public int Status { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public int AvailableStock { get; set; }
}

public class CreateProductDto
{
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? Unit { get; set; }
    public decimal Price { get; set; }
    public string? ImageUrl { get; set; }
    public int SupplierId { get; set; }
    public int InitialStock { get; set; }
}

public class UpdateProductDto
{
    public string? Name { get; set; }
    public string? Category { get; set; }
    public string? Unit { get; set; }
    public decimal? Price { get; set; }
    public string? ImageUrl { get; set; }
    public int? Status { get; set; }
}
