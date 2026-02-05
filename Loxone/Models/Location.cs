using System.ComponentModel.DataAnnotations;

namespace XNDmjApi.Loxone.Models;

public class Location
{
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Indica si la ubicació està activa (disponible per pantalles/relacions).
    /// Default: true.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Ordre opcional per ordenar llistats (null = al final).
    /// </summary>
    public int? SortOrder { get; set; }
}
