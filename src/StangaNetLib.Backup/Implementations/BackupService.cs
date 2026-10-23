using StangaNetLib.Backup.Abstractions;
using StangaNetLib.Backup.Errors;
using StangaNetLib.Backup.Models;
using StangaNetLib.Core.Common;
using StangaNetLib.Core.Guards;
using StangaNetLib.Cryptography.Abstractions;
using StangaNetLib.SchemaRegistry.Abstractions;
using StangaNetLib.SchemaRegistry.Manifests;

namespace StangaNetLib.Backup.Implementations;

/// <summary>Default <see cref="IBackupService"/>: SNEN envelope around a ZIP payload.</summary>
public sealed class BackupService : IBackupService
{
    private readonly IBackupPartExporter _exporter;
    private readonly IBackupPartImporter _importer;
    private readonly IAtRestEncryptionService _encryption;
    private readonly ISchemaValidator? _validator;

    /// <summary>Creates the backup service.</summary>
    public BackupService(
        IBackupPartExporter exporter,
        IBackupPartImporter importer,
        IAtRestEncryptionService encryption,
        ISchemaValidator? validator = null)
    {
        _exporter = Guard.Against.Null(exporter, nameof(exporter));
        _importer = Guard.Against.Null(importer, nameof(importer));
        _encryption = Guard.Against.Null(encryption, nameof(encryption));
        _validator = validator;
    }

    /// <inheritdoc />
    public async Task<Result> CreateBackupAsync(
        string path,
        string passphrase,
        string product,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
            return Result.Failure(BackupErrors.PathRequired());
        if (string.IsNullOrWhiteSpace(passphrase))
            return Result.Failure(BackupErrors.PassphraseRequired());
        if (string.IsNullOrWhiteSpace(product))
            return Result.Failure(BackupErrors.ProductRequired());

        var export = await _exporter.ExportAsync(cancellationToken).ConfigureAwait(false);
        if (export.IsFailure)
            return Result.Failure(BackupErrors.ExportFailed(export.Error.Description));

        var (zipBytes, _) = await BackupArchiveFormat
            .BuildZipAsync(product, export.Value!, cancellationToken)
            .ConfigureAwait(false);

        var encrypt = _encryption.EncryptBlob(zipBytes, passphrase);
        if (encrypt.IsFailure)
            return Result.Failure(BackupErrors.EncryptFailed(encrypt.Error.Description));

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        await File.WriteAllBytesAsync(path, encrypt.Value!, cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> VerifyBackupAsync(
        string path,
        string passphrase,
        CancellationToken cancellationToken = default)
    {
        var opened = await OpenAndDecryptAsync(path, passphrase, cancellationToken).ConfigureAwait(false);
        if (opened.IsFailure)
            return Result.Failure(opened.Error);

        return await ValidateArchiveAsync(opened.Value!.ZipBytes, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Result<BackupMetadata>> GetBackupMetadataAsync(
        string path,
        string passphrase,
        CancellationToken cancellationToken = default)
    {
        var opened = await OpenAndDecryptAsync(path, passphrase, cancellationToken).ConfigureAwait(false);
        if (opened.IsFailure)
            return Result<BackupMetadata>.Failure(opened.Error);

        var validate = await ValidateArchiveAsync(opened.Value!.ZipBytes, cancellationToken).ConfigureAwait(false);
        if (validate.IsFailure)
            return Result<BackupMetadata>.Failure(validate.Error);

        try
        {
            var (manifest, _) = BackupArchiveFormat.ReadZip(opened.Value.ZipBytes);
            var total = manifest.Items.Sum(i => i.SizeBytes ?? 0);
            return Result<BackupMetadata>.Success(
                new BackupMetadata(
                    manifest.Product,
                    manifest.ExportedAt,
                    manifest.SchemaVersion,
                    manifest.Items.Count,
                    total));
        }
        catch (Exception ex)
        {
            return Result<BackupMetadata>.Failure(BackupErrors.ManifestInvalid(ex.Message));
        }
    }

    /// <inheritdoc />
    public async Task<Result> RestoreBackupAsync(
        string path,
        string passphrase,
        CancellationToken cancellationToken = default)
    {
        var opened = await OpenAndDecryptAsync(path, passphrase, cancellationToken).ConfigureAwait(false);
        if (opened.IsFailure)
            return Result.Failure(opened.Error);

        var validate = await ValidateArchiveAsync(opened.Value!.ZipBytes, cancellationToken).ConfigureAwait(false);
        if (validate.IsFailure)
            return Result.Failure(validate.Error);

        BackupManifest manifest;
        Dictionary<string, byte[]> partBytes;
        try
        {
            (manifest, partBytes) = BackupArchiveFormat.ReadZip(opened.Value.ZipBytes);
        }
        catch (Exception ex)
        {
            return Result.Failure(BackupErrors.ManifestInvalid(ex.Message));
        }

        var parts = new List<BackupPart>(manifest.Items.Count);
        var streams = new List<MemoryStream>();
        try
        {
            foreach (var item in manifest.Items)
            {
                if (!partBytes.TryGetValue(item.Id, out var bytes))
                    return Result.Failure(BackupErrors.MissingPart(item.Id));

                var ms = new MemoryStream(bytes, writable: false);
                streams.Add(ms);
                parts.Add(new BackupPart
                {
                    Id = item.Id,
                    Type = item.Type,
                    Content = ms,
                    Checksum = item.Checksum
                });
            }

            return await _importer.ImportAsync(parts, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            foreach (var s in streams)
                await s.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task<Result<OpenedBackup>> OpenAndDecryptAsync(
        string path,
        string passphrase,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
            return Result<OpenedBackup>.Failure(BackupErrors.PathRequired());
        if (string.IsNullOrWhiteSpace(passphrase))
            return Result<OpenedBackup>.Failure(BackupErrors.PassphraseRequired());
        if (!File.Exists(path))
            return Result<OpenedBackup>.Failure(BackupErrors.FileNotFound(path));

        var envelope = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        var decrypt = _encryption.DecryptBlob(envelope, passphrase);
        if (decrypt.IsFailure)
            return Result<OpenedBackup>.Failure(BackupErrors.DecryptFailed(decrypt.Error.Description));

        return Result<OpenedBackup>.Success(new OpenedBackup(decrypt.Value!));
    }

    private async Task<Result> ValidateArchiveAsync(byte[] zipBytes, CancellationToken cancellationToken)
    {
        if (_validator is null)
            return Result.Failure(BackupErrors.ValidatorNotRegistered());

        BackupManifest manifest;
        Dictionary<string, byte[]> parts;
        string manifestJson;
        try
        {
            (manifest, parts) = BackupArchiveFormat.ReadZip(zipBytes);
            manifestJson = System.Text.Json.JsonSerializer.Serialize(manifest, BackupArchiveFormat.JsonOptions);
        }
        catch (Exception ex)
        {
            return Result.Failure(BackupErrors.ManifestInvalid(ex.Message));
        }

        var schema = await _validator
            .ValidateAsync(
                BackupManifestSubjects.BackupManifest,
                BackupManifestSubjects.Version100,
                manifestJson,
                cancellationToken)
            .ConfigureAwait(false);

        if (schema.IsFailure)
            return Result.Failure(schema.Error);

        if (!schema.Value!.IsValid)
            return Result.Failure(
                BackupErrors.ManifestInvalid(string.Join("; ", schema.Value.Errors)));

        foreach (var item in manifest.Items)
        {
            if (!parts.TryGetValue(item.Id, out var bytes))
                return Result.Failure(BackupErrors.MissingPart(item.Id));

            if (!string.IsNullOrEmpty(item.Checksum))
            {
                var actual = BackupArchiveFormat.ComputeSha256Hex(bytes);
                if (!string.Equals(actual, item.Checksum, StringComparison.OrdinalIgnoreCase))
                    return Result.Failure(BackupErrors.ChecksumMismatch(item.Id));
            }
        }

        return Result.Success();
    }

    private sealed record OpenedBackup(byte[] ZipBytes);
}
