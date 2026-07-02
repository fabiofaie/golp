namespace Golp.Api.Data.Entities;

public class Sport
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PointUnit { get; set; } = string.Empty;
    public bool Sets { get; set; }
    public int TeamSize { get; set; }
    public bool IsActive { get; set; } = true;
    public double SetWeight { get; set; }
    public bool AllowsSingles { get; set; }
}
