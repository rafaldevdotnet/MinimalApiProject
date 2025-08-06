using MinimalApiProject;
using System.Data;
using System.Data.SqlClient;



var builder = WebApplication.CreateBuilder(args);

// Inicjalizacja ustawie� aplikacji
AppSettings.Initialize(builder.Configuration);

// Konfiguracja po��czenia z baz� danych
builder.Services.AddScoped<IDbConnection>(_ => new SqlConnection(AppSettings.ConnectionString));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Rejestracja repozytorium jako zale�no�ci
builder.Services.AddScoped<ProductRepository>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Endpoint do importu danych CSV do bazy danych
app.MapPost("/import-data", async (ProductRepository repo) =>
{
    DateTime startTime = DateTime.Now;
    await repo.InitDb();//Inicjalizacja bazy danych
    await repo.ImportDataAsync();//Pobieranie plik�w csv i importowanie danych do bazy  
    TimeSpan duration = DateTime.Now - startTime;
    return Results.Ok($"Import completed in {duration.TotalSeconds.ToString("#0.00")} seconds");
});

// Endpoint do pobrania szczeg��w produktu na podstawie SKU
app.MapGet("/product/{sku}", async (string sku, ProductRepository repo) =>
{
    var product = await repo.GetProductDetailsAsync(sku);
    return product is not null ? Results.Ok(product) : Results.NotFound();
});

app.Run();