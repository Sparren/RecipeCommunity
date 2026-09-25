using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Recipe.Infrastructure.Persistence;

namespace Recipe.IntegrationTests;

public sealed class PostgresFixture : IAsyncLifetime
{
    public string ConnectionString { get; private set; } = "";
    public WebApplicationFactory<Program> Factory { get; private set; } = null!;
    private readonly string databaseName = "recipe_test_" + Guid.NewGuid().ToString("N");
    private readonly string imageDirectory = Path.Combine(Path.GetTempPath(), "recipe-images-" + Guid.NewGuid().ToString("N"));
    private string AdminConnection => Environment.GetEnvironmentVariable("RECIPE_TEST_CONNECTION") ?? "Host=localhost;Port=54329;Database=postgres;Username=recipe;Password=recipe_local";
    public RecipeDbContext CreateDb() => new(new DbContextOptionsBuilder<RecipeDbContext>().UseNpgsql(ConnectionString).Options);
    public async Task InitializeAsync()
    {
        await using var admin = new NpgsqlConnection(AdminConnection);
        await admin.OpenAsync();
        await using (var command = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", admin)) await command.ExecuteNonQueryAsync();
        var connection = new NpgsqlConnectionStringBuilder(AdminConnection) { Database = databaseName };
        ConnectionString = connection.ConnectionString;
        await using var db = CreateDb();
        await db.Database.MigrateAsync();
        await DevelopmentSeed.Run(db);
        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
        {
            host.UseEnvironment("Testing");
            host.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Recipe"] = ConnectionString,
                ["Images:Directory"] = imageDirectory
            }));
        });
    }
    public async Task DisposeAsync()
    {
        if (Factory is not null) await Factory.DisposeAsync();
        NpgsqlConnection.ClearAllPools();
        await using var admin = new NpgsqlConnection(AdminConnection);
        await admin.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)", admin);
        await command.ExecuteNonQueryAsync();
        if (Directory.Exists(imageDirectory)) Directory.Delete(imageDirectory, true);
    }
}

[CollectionDefinition("Postgres")]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;
