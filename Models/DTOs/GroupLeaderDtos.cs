namespace FreshGroupSystem.Models.DTOs;

public class GroupLeaderDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? CommunityName { get; set; }
    public string? Address { get; set; }
    public int Status { get; set; }
    public int OrderCount { get; set; }
}

public class CreateGroupLeaderDto
{
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? CommunityName { get; set; }
    public string? Address { get; set; }
}
