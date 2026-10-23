namespace StangaNetLib.Backup.Models;

/// <summary>Non-sensitive metadata extracted from a verified backup archive.</summary>
/// <param name="Product">Product name from the manifest.</param>
/// <param name="ExportedAt">Export timestamp.</param>
/// <param name="SchemaVersion">Manifest schema version.</param>
/// <param name="ItemCount">Number of parts.</param>
/// <param name="TotalSizeBytes">Sum of part sizes when known.</param>
public sealed record BackupMetadata(
    string Product,
    DateTimeOffset ExportedAt,
    string SchemaVersion,
    int ItemCount,
    long TotalSizeBytes);
