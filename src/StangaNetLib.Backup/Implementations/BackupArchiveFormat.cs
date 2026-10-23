using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StangaNetLib.Backup.Models;
using StangaNetLib.SchemaRegistry.Manifests;

namespace StangaNetLib.Backup.Implementations;

/// <summary>ZIP + manifest helpers for the portable backup payload (inside SNEN).</summary>
internal static class BackupArchiveFormat
{
    internal const string ManifestEntryName = "manifest.json";
    internal const string PartsPrefix = "parts/";

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    internal static string ComputeSha256Hex(byte[] data)
    {
        var hash = SHA256.HashData(data);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    internal static async Task<(byte[] ZipBytes, BackupManifest Manifest)> BuildZipAsync(
        string product,
        IReadOnlyList<BackupPart> parts,
        CancellationToken cancellationToken)
    {
        var items = new List<BackupManifestItem>();
        await using var zipStream = new MemoryStream();
        BackupManifest manifest;

        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var part in parts)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await using var buffer = new MemoryStream();
                await part.Content.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
                var bytes = buffer.ToArray();
                var checksum = part.Checksum ?? ComputeSha256Hex(bytes);
                var entryPath = PartsPrefix + SanitizeId(part.Id);

                var entry = archive.CreateEntry(entryPath, CompressionLevel.Optimal);
                await using (var entryStream = entry.Open())
                {
                    await entryStream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                }

                items.Add(new BackupManifestItem
                {
                    Type = part.Type,
                    Id = part.Id,
                    Path = entryPath,
                    Checksum = checksum,
                    SizeBytes = bytes.LongLength
                });
            }

            manifest = new BackupManifest
            {
                SchemaVersion = BackupManifestSubjects.Version100,
                ExportedAt = DateTimeOffset.UtcNow,
                Product = product,
                Items = items,
                Encryption = new BackupManifestEncryption
                {
                    Algorithm = "AES-256-GCM",
                    Kdf = "PBKDF2-SHA256"
                },
                ContentHash = ComputeSha256Hex(
                    Encoding.UTF8.GetBytes(string.Join('|', items.Select(i => i.Checksum))))
            };

            var manifestJson = JsonSerializer.Serialize(manifest, JsonOptions);
            var manifestEntry = archive.CreateEntry(ManifestEntryName, CompressionLevel.Optimal);
            await using (var entryStream = manifestEntry.Open())
            await using (var writer = new StreamWriter(entryStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
            {
                await writer.WriteAsync(manifestJson.AsMemory(), cancellationToken).ConfigureAwait(false);
            }
        }

        return (zipStream.ToArray(), manifest);
    }

    internal static (BackupManifest Manifest, Dictionary<string, byte[]> Parts) ReadZip(byte[] zipBytes)
    {
        using var zipStream = new MemoryStream(zipBytes, writable: false);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

        var manifestEntry = archive.GetEntry(ManifestEntryName)
            ?? throw new InvalidDataException("Archive is missing manifest.json.");

        string manifestJson;
        using (var reader = new StreamReader(manifestEntry.Open(), Encoding.UTF8))
            manifestJson = reader.ReadToEnd();

        var manifest = JsonSerializer.Deserialize<BackupManifest>(manifestJson, JsonOptions)
            ?? throw new InvalidDataException("manifest.json could not be deserialized.");

        var parts = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var item in manifest.Items)
        {
            var path = item.Path ?? PartsPrefix + SanitizeId(item.Id);
            var entry = archive.GetEntry(path)
                ?? throw new InvalidDataException($"Missing part entry '{path}'.");

            using var ms = new MemoryStream();
            using (var es = entry.Open())
                es.CopyTo(ms);
            parts[item.Id] = ms.ToArray();
        }

        return (manifest, parts);
    }

    private static string SanitizeId(string id)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            id = id.Replace(c, '_');
        return id.Replace('/', '_').Replace('\\', '_');
    }
}
