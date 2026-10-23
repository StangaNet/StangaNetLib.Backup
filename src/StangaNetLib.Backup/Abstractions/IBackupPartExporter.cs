using StangaNetLib.Backup.Models;
using StangaNetLib.Core.Common;

namespace StangaNetLib.Backup.Abstractions;

/// <summary>
/// Consumer-provided port that exports application data streams to include in a backup.
/// </summary>
public interface IBackupPartExporter
{
    /// <summary>Exports parts to include in the archive. Streams must remain readable until create finishes.</summary>
    Task<Result<IReadOnlyList<BackupPart>>> ExportAsync(CancellationToken cancellationToken = default);
}
