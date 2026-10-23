using StangaNetLib.Backup.Models;
using StangaNetLib.Core.Common;

namespace StangaNetLib.Backup.Abstractions;

/// <summary>
/// Consumer-provided port that applies restored parts to the target instance.
/// This operation is <b>destructive</b> relative to target state — policy is owned by the consumer.
/// </summary>
public interface IBackupPartImporter
{
    /// <summary>Imports parts after the library has verified the archive.</summary>
    Task<Result> ImportAsync(
        IReadOnlyList<BackupPart> parts,
        CancellationToken cancellationToken = default);
}
