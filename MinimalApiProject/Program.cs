using MinimalApiProject;
using System.Data;
using System.Data.SqlClient;



var builder = WebApplication.CreateBuilder(args);
AppSettings.Initialize(builder.Configuration);
var connStr = AppSettings.ConnectionString;
builder.Services.AddScoped<IDbConnection>(_ => new SqlConnection(connStr));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<ProductRepository>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
}


app.MapPost("/import-data", async (ProductRepository repo) =>
{
    DateTime startTime = DateTime.Now;
    await repo.InitDb();
    await repo.ImportDataAsync();
    TimeSpan duration = DateTime.Now - startTime;
    return Results.Ok($"Import completed in {duration.TotalSeconds.ToString("#0.00")} seconds");
});


app.MapGet("/product/{sku}", async (string sku, ProductRepository repo) =>
{
    var product = await repo.GetProductDetailsAsync(sku);
    return product is not null ? Results.Ok(product) : Results.NotFound();
});

app.Run();