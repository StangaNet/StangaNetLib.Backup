using Microsoft.Extensions.DependencyInjection;
using StangaNetLib.Backup.Abstractions;
using StangaNetLib.Backup.Extensions;
using StangaNetLib.SchemaRegistry.Extensions;

namespace StangaNetLib.Backup.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public async Task AddStangaNetLibBackup_RegistersService()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IBackupPartExporter>(new FakeExporter());
        services.AddSingleton<IBackupPartImporter>(new FakeImporter());
        services.AddStangaNetLibInMemorySchemaRegistry();
        services.AddStangaNetLibBackup();

        var sp = services.BuildServiceProvider();
        await sp.AddStangaNetLibBackupManifestSchemasAsync();

        sp.GetService<IBackupService>().Should().NotBeNull();
    }
}
