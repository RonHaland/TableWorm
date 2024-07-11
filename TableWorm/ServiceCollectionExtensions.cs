using Azure.Core;
using Azure.Data.Tables;
using Microsoft.Extensions.DependencyInjection;

namespace TableWorm;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTableStorage(this IServiceCollection services, string connectionString, Action<ContextConfigurationBuilder> configuration)
    {
        var configBuilder = new ContextConfigurationBuilder(connectionString);
        configuration(configBuilder);
        var tableContext = configBuilder.Build();

        return services.AddSingleton(tableContext);
    }

    public static IServiceCollection AddTableStorage(this IServiceCollection services, TokenCredential creds, string endpoint, Action<ContextConfigurationBuilder> configuration)
    {
        var configBuilder = new ContextConfigurationBuilder(creds, endpoint);
        configuration(configBuilder);
        var tableContext = configBuilder.Build();

        return services.AddSingleton(tableContext);
    }

    public static IServiceCollection AddTableStorage(this IServiceCollection services, Action<ContextCredentialBuilder> configuration)
    {
        var configBuilder = new ContextCredentialBuilder();
        configuration(configBuilder);
        var tableContext = configBuilder.ResultingContext.Build();

        return services.AddSingleton(tableContext);
    }
}

public class ContextCredentialBuilder
{
    TableStorage _ctx = new();
    internal ContextConfigurationBuilder ResultingContext { get; set; } = null!;

    public ContextConfigurationBuilder ConfigureConnectionString(string connectionString)
    {
        ResultingContext = new ContextConfigurationBuilder(connectionString);
        return ResultingContext;
    }

    public ContextConfigurationBuilder ConfigureTokenCredential(TokenCredential creds, string endpoint)
    {
        ResultingContext = new ContextConfigurationBuilder(creds, endpoint);
        return ResultingContext;
    }
}

public class ContextConfigurationBuilder
{
    TableStorage _ctx;
    public ContextConfigurationBuilder(string connectionString)
    {
        _ctx = new();
        _ctx.ConfigureConnectionString(connectionString);
    }
    public ContextConfigurationBuilder(TokenCredential creds, string endpoint)
    {
        _ctx = new();
        _ctx.ConfigureTokenCredential(creds, endpoint);
    }

    public ContextConfigurationBuilder AddTable<TTable>() where TTable : TableModel
    {
        _ctx.RegisterTable<TTable>();
        return this;
    }

    public ContextConfigurationBuilder AddClientOptions(TableClientOptions opts)
    {
        _ctx.ConfigureClientOptions(opts);
        return this;
    }

    internal TableStorage Build()
    {
        return _ctx;
    }
}
