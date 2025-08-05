//using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using MinimalApiProject;
using MinimalApiProject.Enums;
using Npgsql;
using System.Data;
using System.Data.SqlClient;



var builder = WebApplication.CreateBuilder(args);
AppSettings.Initialize(builder.Configuration);
var connStr = AppSettings.ConnectionString;
builder.Services.AddScoped<IDbConnection>(_ =>
{

    return AppSettings.DatabaseType switch
    {
        DbTypeEnum.MSSQL => new SqlConnection(connStr),
        DbTypeEnum.Sqlite => new SqliteConnection(connStr),
        DbTypeEnum.Postgres => new NpgsqlConnection(connStr),
        _ => throw new NotSupportedException()
    };
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<ProductRepository>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapPost("/import-data", async (ProductRepository repo) =>
{
    await repo.ImportDataAsync();
    return Results.Ok("Import completed.");
});


app.MapGet("/product/{sku}", async (string sku, ProductRepository repo) =>
{
    var product = await repo.GetProductDetailsAsync(sku);
    return product is not null ? Results.Ok(product) : Results.NotFound();
});

app.Run();