using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StangaNetLib.Backup.Abstractions;
using StangaNetLib.Backup.Implementations;
using StangaNetLib.Cryptography.Abstractions;
using StangaNetLib.Cryptography.Extensions;
using StangaNetLib.SchemaRegistry.Abstractions;

namespace StangaNetLib.Backup.Extensions;

/// <summary>DI registration for StangaNetLib.Backup.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IBackupService"/>.
    /// Requires consumer registrations of <see cref="IBackupPartExporter"/> and <see cref="IBackupPartImporter"/>.
    /// Also registers Cryptography. Register SchemaRegistry + call
    /// <c>AddStangaNetLibBackupManifestSchemasAsync</c> on the built provider before verify/restore.
    /// </summary>
    public static IServiceCollection AddStangaNetLibBackup(this IServiceCollection services)
    {
        services.AddStangaNetLibCryptography();
        services.TryAddSingleton<IBackupService>(sp =>
            new BackupService(
                sp.GetRequiredService<IBackupPartExporter>(),
                sp.GetRequiredService<IBackupPartImporter>(),
                sp.GetRequiredService<IAtRestEncryptionService>(),
                sp.GetService<ISchemaValidator>()));
        return services;
    }

    /// <summary>
    /// Registers <see cref="IBackupService"/> (same as parameterless) after optionally binding configuration
    /// for Cryptography settings.
    /// </summary>
    public static IServiceCollection AddStangaNetLibBackup(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddStangaNetLibCryptography(configuration);
        services.TryAddSingleton<IBackupService>(sp =>
            new BackupService(
                sp.GetRequiredService<IBackupPartExporter>(),
                sp.GetRequiredService<IBackupPartImporter>(),
                sp.GetRequiredService<IAtRestEncryptionService>(),
                sp.GetService<ISchemaValidator>()));
        return services;
    }
}
