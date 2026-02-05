namespace XNDmjApi.Loxone.Models;

public class LocationAction
{
    public int Id { get; set; }

    public int LocationId { get; set; }
    public Location Location { get; set; } = default!;

    public int LoxoneActionId { get; set; }
    public LoxoneAction LoxoneAction { get; set; } = default!;

    public bool IsDefault { get; set; }
}