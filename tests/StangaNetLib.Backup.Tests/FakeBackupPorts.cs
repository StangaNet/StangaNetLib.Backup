using System.Text;
using StangaNetLib.Backup.Abstractions;
using StangaNetLib.Backup.Models;
using StangaNetLib.Core.Common;

namespace StangaNetLib.Backup.Tests;

internal sealed class FakeExporter : IBackupPartExporter
{
    private readonly byte[] _payload;

    public FakeExporter(string text = "hello-backup")
    {
        _payload = Encoding.UTF8.GetBytes(text);
    }

    public Task<Result<IReadOnlyList<BackupPart>>> ExportAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<BackupPart> parts =
        [
            new BackupPart
            {
                Id = "db",
                Type = "database",
                Content = new MemoryStream(_payload, writable: false)
            }
        ];
        return Task.FromResult(Result<IReadOnlyList<BackupPart>>.Success(parts));
    }
}

internal sealed class FakeImporter : IBackupPartImporter
{
    public List<(string Id, string Type, byte[] Bytes)> Imported { get; } = [];

    public async Task<Result> ImportAsync(
        IReadOnlyList<BackupPart> parts,
        CancellationToken cancellationToken = default)
    {
        foreach (var part in parts)
        {
            await using var ms = new MemoryStream();
            await part.Content.CopyToAsync(ms, cancellationToken);
            Imported.Add((part.Id, part.Type, ms.ToArray()));
        }

        return Result.Success();
    }
}
