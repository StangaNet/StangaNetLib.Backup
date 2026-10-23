using StangaNetLib.Core.Common;

namespace StangaNetLib.Backup.Errors;

internal static class BackupErrors
{
    internal static Error PassphraseRequired()
        => Error.Validation("Passphrase", "A non-empty backup passphrase is required.");

    internal static Error PathRequired()
        => Error.Validation("Path", "Backup path must not be null or whitespace.");

    internal static Error ProductRequired()
        => Error.Validation("Product", "Product name must not be null or whitespace.");

    internal static Error FileNotFound(string path)
        => Error.NotFound("BackupFile", path);

    internal static Error DecryptFailed(string description)
        => Error.Validation("Integrity", $"Backup decrypt failed: {description}");

    internal static Error ManifestInvalid(string description)
        => Error.Validation("Manifest", description);

    internal static Error ChecksumMismatch(string partId)
        => Error.Validation("Checksum", $"Checksum mismatch for backup part '{partId}'.");

    internal static Error MissingPart(string partId)
        => Error.Validation("Part", $"Missing archive part '{partId}'.");

    internal static Error ValidatorNotRegistered()
        => Error.Validation(
            "SchemaValidator",
            "ISchemaValidator is not registered. Call AddStangaNetLibInMemorySchemaRegistry (or equivalent) and AddStangaNetLibBackupManifestSchemasAsync.");

    internal static Error ExportFailed(string description)
        => Error.Internal($"Backup export failed: {description}");

    internal static Error EncryptFailed(string description)
        => Error.Internal($"Backup encryption failed: {description}");
}
