namespace SaPosts.Web.Media;

public static class MediaServiceCollectionExtensions
{
    public static IServiceCollection AddMedia(this IServiceCollection services)
    {
        services.AddOptions<MediaOptions>()
            .BindConfiguration(MediaOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IMediaStorage, LocalMediaStorage>();
        services.AddSingleton<MediaStaging>();
        services.AddSingleton<ImageProcessor>();
        services.AddScoped<MediaUploader>();
        services.AddScoped<IMediaCatalog, MediaCatalog>();
        services.AddScoped<MediaLibrary>();
        return services;
    }
}
