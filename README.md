# StangaNetLib.Backup

[![.NET CI](https://github.com/StangaNet/StangaNetLib.Backup/actions/workflows/main.yml/badge.svg)](https://github.com/StangaNet/StangaNetLib.Backup/actions/workflows/main.yml)
![NuGet](https://img.shields.io/badge/nuget-1.0.0-blue)
![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%209.0%20%7C%2010.0-512BD4)

Create, verify, and restore **encrypted portable backups** for self-hosted apps. Archives are SNEN-encrypted ZIP files with a versioned manifest validated by [`StangaNetLib.SchemaRegistry`](https://github.com/StangaNet/StangaNetLib.SchemaRegistry).

## Archive format (`.snbak`)

1. Outer layer: Cryptography **SNEN v1** envelope (user passphrase, PBKDF2 + AES-256-GCM)
2. Inner payload: ZIP containing:
   - `manifest.json` — subject `stanganet.backup.manifest` / `1.0.0`
   - `parts/{id}` — consumer-exported streams with SHA-256 checksums

## Quick Start

```csharp
builder.Services.AddStangaNetLibInMemorySchemaRegistry();
builder.Services.AddSingleton<IBackupPartExporter, MyExporter>();
builder.Services.AddSingleton<IBackupPartImporter, MyImporter>();
builder.Services.AddStangaNetLibBackup(builder.Configuration);

var app = builder.Build();
await app.Services.AddStangaNetLibBackupManifestSchemasAsync();

var backups = app.Services.GetRequiredService<IBackupService>();
await backups.CreateBackupAsync("instance.snbak", passphrase, product: "MyApp");
await backups.VerifyBackupAsync("instance.snbak", passphrase);
await backups.RestoreBackupAsync("instance.snbak", passphrase); // destructive — consumer policy
```

## vs EncryptedStorage

| Library | Role |
|---|---|
| EncryptedStorage | Runtime encrypted blobs for the live app |
| Backup | Portable snapshot of an instance (export/import hooks) |

## License

Apache-2.0. See [LICENSE](./LICENSE).
