using StangaNetLib.Backup.Models;
using StangaNetLib.Core.Common;

namespace StangaNetLib.Backup.Abstractions;

/// <summary>
/// Creates, verifies, inspects, and restores encrypted portable backup archives (`.snbak`).
/// </summary>
public interface IBackupService
{
    /// <summary>
    /// Creates an encrypted backup at <paramref name="path"/> using the consumer exporter.
    /// </summary>
    Task<Result> CreateBackupAsync(
        string path,
        string passphrase,
        string product,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Decrypts and validates the archive (manifest schema + part checksums) without importing.
    /// </summary>
    Task<Result> VerifyBackupAsync(
        string path,
        string passphrase,
        CancellationToken cancellationToken = default);

    /// <summary>Returns metadata after a successful decrypt + structural read.</summary>
    Task<Result<BackupMetadata>> GetBackupMetadataAsync(
        string path,
        string passphrase,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies then imports. Restore is blocked when verify fails.
    /// </summary>
    Task<Result> RestoreBackupAsync(
        string path,
        string passphrase,
        CancellationToken cancellationToken = default);
}
