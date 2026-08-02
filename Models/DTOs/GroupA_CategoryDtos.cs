namespace FreshColdChain.Models.DTOs;

public class CategoryDto
{
    public string CategoryID { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string? ParentID { get; set; }
}

public class CreateCategoryDto
{
    public string CategoryName { get; set; } = string.Empty;
    public string? ParentID { get; set; }
}
