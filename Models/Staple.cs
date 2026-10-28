using System.ComponentModel.DataAnnotations;

namespace RecipesApp.Models;

/// <summary>
/// An ingredient the cook always has on hand (salt, oil). The "made from only these" search
/// lets recipes use staples without them being picked. Matched to ingredients by name, so a
/// staple can be listed before any recipe uses it.
/// </summary>
public class Staple
{
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;
}
