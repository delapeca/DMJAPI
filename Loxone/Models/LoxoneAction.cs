using System.ComponentModel.DataAnnotations;

namespace XNDmjApi.Loxone.Models;

public class LoxoneAction
{
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string LoxoneCommand { get; set; } = string.Empty;
}