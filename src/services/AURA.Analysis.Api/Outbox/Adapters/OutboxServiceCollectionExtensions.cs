using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace AURA.Analysis.Api.Outbox.Adapters;

public static class OutboxServiceCollectionExtensions
{
    /// <summary>Dang ky publisher. Goi trong Program.cs: builder.Services.AddOutboxPublisher(builder.Configuration);
    /// Chi dang ky khi Outbox:Enabled=true. Connection string rieng: ConnectionStrings:OutboxPublisher (role aura_analysis_publisher).</summary>
    public static IServiceCollection AddOutboxPublisher(this IServiceCollection services, IConfiguration cfg)
    {
        services.Configure<OutboxOptions>(cfg.GetSection(OutboxOptions.Section));
        var opt = cfg.GetSection(OutboxOptions.Section).Get<OutboxOptions>() ?? new OutboxOptions();
        if (!opt.Enabled) return services;
        opt.Validate(); // sai cau hinh -> app khong khoi dong
        services.Configure<RabbitMqOptions>(cfg.GetSection(RabbitMqOptions.Section));
        var cs = cfg.GetConnectionString("OutboxPublisher")
                 ?? throw new InvalidOperationException("Thieu ConnectionStrings:OutboxPublisher");
        services.AddSingleton(_ => new OutboxDataSource(NpgsqlDataSource.Create(cs)));
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IOutboxStore, NpgsqlOutboxStore>();
        services.AddSingleton<IBrokerPublisher, RabbitMqBrokerPublisher>();
        services.AddSingleton<OutboxPublisher>();
        services.AddHostedService<OutboxPublisherService>();
        return services;
    }
}
