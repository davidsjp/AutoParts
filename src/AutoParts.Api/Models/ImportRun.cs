using System.ComponentModel.DataAnnotations;

namespace AutoParts.Api.Models;

public sealed class ImportRun
{
    public int Id { get; set; }
    [MaxLength(100)] public required string Source { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    [MaxLength(50)] public required string Status { get; set; }
    public int RecordsProcessed { get; set; }
    [MaxLength(4000)] public string? ErrorMessage { get; set; }
}
