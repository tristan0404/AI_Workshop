using System.ComponentModel.DataAnnotations;
using AI_Workshop.Models.Identity;

namespace AI_Workshop.Models.Academic;

public sealed class OfficeHour
{
    public int Id { get; set; }
    public string LecturerId { get; set; } = string.Empty;
    public ApplicationUser Lecturer { get; set; } = null!;
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly StartsAt { get; set; }
    public TimeOnly EndsAt { get; set; }
    [Required, StringLength(200)] public string Location { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
