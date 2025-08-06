using MinimalApiProject;
using System.Data;
using System.Data.SqlClient;



var builder = WebApplication.CreateBuilder(args);

// Inicjalizacja ustawieñ aplikacji
AppSettings.Initialize(builder.Configuration);

// Konfiguracja po³¹czenia z baz¹ danych
builder.Services.AddScoped<IDbConnection>(_ => new SqlConnection(AppSettings.ConnectionString));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Rejestracja repozytorium jako zale¿noœci
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
    await repo.ImportDataAsync();//Pobieranie plików csv i importowanie danych do bazy  
    TimeSpan duration = DateTime.Now - startTime;
    return Results.Ok($"Import completed in {duration.TotalSeconds.ToString("#0.00")} seconds");
});

// Endpoint do pobrania szczegó³ów produktu na podstawie SKU
app.MapGet("/product/{sku}", async (string sku, ProductRepository repo) =>
{
    var product = await repo.GetProductDetailsAsync(sku);
    return product is not null ? Results.Ok(product) : Results.NotFound();
});

app.Run();