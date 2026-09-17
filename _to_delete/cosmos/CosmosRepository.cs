using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;

namespace FromTheFarm.Api.Services;

// Thin generic wrapper over a single Cosmos container. Kept deliberately
// simple (no Unit of Work, no query specification pattern) — the project
// only has five containers and each has genuinely different access patterns,
// so most controllers query LINQ directly against Container rather than
// hiding it behind an over-abstracted repository interface.
public class CosmosRepository<T> where T : class
{
    private readonly Container _container;

    public CosmosRepository(CosmosClient client, IOptions<CosmosDbOptions> options, string containerName, string partitionKeyPath)
    {
        var database = client.GetDatabase(options.Value.DatabaseName);
        _container = database.CreateContainerIfNotExistsAsync(containerName, partitionKeyPath)
            .GetAwaiter().GetResult().Container;
    }

    public Container Container => _container;

    public async Task<T?> GetByIdAsync(string id, string partitionKeyValue)
    {
        try
        {
            var response = await _container.ReadItemAsync<T>(id, new PartitionKey(partitionKeyValue));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<T> UpsertAsync(T item, string partitionKeyValue)
    {
        var response = await _container.UpsertItemAsync(item, new PartitionKey(partitionKeyValue));
        return response.Resource;
    }

    public async Task DeleteAsync(string id, string partitionKeyValue)
    {
        await _container.DeleteItemAsync<T>(id, new PartitionKey(partitionKeyValue));
    }
}
