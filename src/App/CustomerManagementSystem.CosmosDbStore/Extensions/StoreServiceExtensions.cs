using CustomerManagementSystem.CosmosDbStore.Serializers;
using CustomerManagementSystem.Domain;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;

namespace CustomerManagementSystem.CosmosDbStore.Extensions;

public static class StoreServiceExtensions
{
    public static void AddStore(this IServiceCollection services, string connectionString)
    {
        services.AddScoped<IEventStore>(_ =>
        {
            var cosmosClient = new CosmosClient(connectionString, new CosmosClientOptions
            {
                ApplicationName = ".NET Days Warsaw - Roslyn",
                EnableContentResponseOnWrite = false,

                Serializer = new CosmosSystemTextJsonSerializer(),
            });

            var database = cosmosClient.GetDatabase("dotnet-warsaw-demo");
            var container = database.GetContainer("Customers");

            return new CosmosEventStore(container);
        });
    }
}
