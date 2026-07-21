using System;

namespace NovaLauncher.Models;

public class Game
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string ExecutablePath { get; set; } = string.Empty;

    public string? CoverImagePath { get; set; }

    public DateTime AddedAt { get; set; } = DateTime.Now;
}