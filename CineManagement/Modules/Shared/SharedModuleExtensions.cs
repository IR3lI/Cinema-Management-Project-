using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Amazon.S3;
using Amazon.Runtime;
using CineManagement.Modules.Shared.Storage;
using CineManagement.Modules.Shared.Common;

namespace CineManagement.Modules.Shared;

public static class SharedModuleExtensions
{
    public static IServiceCollection AddSharedModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
        services.AddSingleton<IAmazonS3>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<StorageOptions>>().Value;
            var config = new AmazonS3Config
            {
                ServiceURL = opts.ServiceUrl,
                ForcePathStyle = opts.ForcePathStyle,
                UseHttp = opts.ServiceUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            };
            var credentials = new BasicAWSCredentials(opts.AccessKeyId, opts.SecretAccessKey);
            return new AmazonS3Client(credentials, config);
        });

        services.AddScoped<IStorageService, S3StorageService>();
        services.AddScoped<IFileStorageService, FileStorageService>();
        services.AddSingleton<IEventBus, InMemoryEventBus>();
        return services;
    }
}
