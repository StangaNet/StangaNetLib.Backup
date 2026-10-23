namespace StangaNetLib.Backup.Models;

/// <summary>
/// A single application data part contributed to (or restored from) a backup archive.
/// The caller owns <see cref="Content"/> and should dispose it after export/import completes.
/// </summary>
public sealed class BackupPart
{
    /// <summary>Stable part identifier (used as ZIP entry name under <c>parts/</c>).</summary>
    public required string Id { get; init; }

    /// <summary>Logical type (e.g. <c>db</c>, <c>blob</c>, <c>settings</c>).</summary>
    public required string Type { get; init; }

    /// <summary>Part payload stream.</summary>
    public required Stream Content { get; init; }

    /// <summary>Optional precomputed SHA-256 hex checksum; computed during create when null.</summary>
    public string? Checksum { get; set; }
}
