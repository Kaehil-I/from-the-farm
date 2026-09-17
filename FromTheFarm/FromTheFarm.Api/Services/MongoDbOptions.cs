namespace FromTheFarm.Api.Services;

public class MongoDbOptions
{
    // Atlas SRV connection string. Supplied by user-secrets when running
    // locally and by an environment variable in the deployed environment.
    // Never commit a real value.
    public string ConnectionString { get; set; } = string.Empty;

    public string DatabaseName { get; set; } = string.Empty;
}
