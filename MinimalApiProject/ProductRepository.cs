using CsvHelper;
using CsvHelper.Configuration;
using Dapper;
using MinimalApiProject.ModelsCsv;
using System.Data;
using System.Data.SqlClient;
using System.Formats.Asn1;
using System.Globalization;

namespace MinimalApiProject
{
    

    public class ProductRepository
    {
        private readonly IDbConnection _db;
        private readonly string _basePath = "./bin/Data";

        public ProductRepository(IDbConnection db)
        {
            _db = db;            
            Directory.CreateDirectory(_basePath);
            InitDb();
        }




        public void InitDb()
        {
            _db.Execute(@"
                        IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Products' AND xtype='U')
                        CREATE TABLE Products (
                            Id INT PRIMARY KEY,
                            SKU NVARCHAR(100) NOT NULL,
                            Name NVARCHAR(500),
                            EAN NVARCHAR(100),
                            ProducerName NVARCHAR(500),
                            Category NVARCHAR(1000),
                            DefaultImage NVARCHAR(500)
                        );
                        
                        IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Inventory' AND xtype='U')
                        CREATE TABLE Inventory (
                            Id INT IDENTITY PRIMARY KEY,
                            ProductId INT,
                            SKU NVARCHAR(100),
                            Unit NVARCHAR(50),
                            Qty INT,
                            ShippingCost DECIMAL(18,2)
                        );
                        
                        IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Prices' AND xtype='U')
                        CREATE TABLE Prices (
                            Id INT IDENTITY PRIMARY KEY,
                            SKU NVARCHAR(100),
                            PricePerUnit DECIMAL(18,2),
                            PricePerLogisticUnit DECIMAL(18,2)
                        );");
        }


        public async Task ImportDataAsync()
        {
            var productsFile = Path.Combine(_basePath, "Products.csv");
            var inventoryFile = Path.Combine(_basePath, "Inventory.csv");
            var pricesFile = Path.Combine(_basePath, "Prices.csv");

            using var httpClient = new HttpClient();
            await File.WriteAllBytesAsync(productsFile, await httpClient.GetByteArrayAsync(AppSettings.GetCsvUrl("Products")));
            await File.WriteAllBytesAsync(inventoryFile, await httpClient.GetByteArrayAsync(AppSettings.GetCsvUrl("Inventory")));
            await File.WriteAllBytesAsync(pricesFile, await httpClient.GetByteArrayAsync(AppSettings.GetCsvUrl("Prices")));
            httpClient.Dispose();


            var productMap = new Dictionary<string, int>();

            // Load Products
            var productList = new List<ProductCsv>();
            using (var reader1 = new StreamReader(productsFile))
            using (var csv1 = new CsvReader(reader1, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = ";",
                HeaderValidated = null,
                MissingFieldFound = null,
                BadDataFound = null
            }))
            {
                var records1 = csv1.GetRecords<ProductCsv>().Where(x =>
                    int.TryParse(x.shipping?.Trim().TrimEnd('h'), out var h1) && h1 <= 24 && x.is_wire == "0" && x.available == "1");

                foreach (var r in records1)
                {
                    productList.Add(r);
                    if (!productMap.ContainsKey(r.SKU))
                        productMap[r.SKU] = int.Parse(r.ID);
                }
            }

            BulkInsert(productList.Select(r => new
            {
                Id = int.Parse(r.ID),
                r.SKU,
                Name = r.name,
                r.EAN,
                ProducerName = r.producer_name,
                Category = r.category,
                DefaultImage = r.default_image
            }), "Products");

            using var reader2 = new StreamReader(inventoryFile);
            using var csv2 = new CsvReader(reader2, new CsvConfiguration(CultureInfo.InvariantCulture) { HeaderValidated = null, MissingFieldFound = null });
            var records2 = csv2.GetRecords<dynamic>().Where(x => int.Parse(x.shipping) <= 24 && productMap.ContainsKey(x.sku));
            foreach (var r in records2)
            {
                await _db.ExecuteAsync("INSERT INTO Inventory (ProductId, SKU, Unit, Qty, ShippingCost) VALUES (@ProductId, @SKU, @Unit, @Qty, @ShippingCost)", new
                {
                    ProductId = productMap[r.sku],
                    SKU = r.sku,
                    Unit = r.unit,
                    Qty = int.Parse(r.qty),
                    ShippingCost = decimal.Parse(r.shipping_cost)
                });
            }

            using var reader3 = new StreamReader(pricesFile);
            using var csv3 = new CsvReader(reader3, new CsvConfiguration(CultureInfo.InvariantCulture) { HeaderValidated = null, MissingFieldFound = null, HasHeaderRecord = false });
            var records3 = csv3.GetRecords<dynamic>();
            foreach (var r in records3)
            {
                await _db.ExecuteAsync("INSERT INTO Prices (SKU, PricePerUnit, PricePerLogisticUnit) VALUES (@SKU, @PPU, @PPLU)", new
                {
                    SKU = r.Column2,
                    PPU = decimal.Parse(r.Column3),
                    PPLU = decimal.Parse(r.Column6)
                });
            }
        }

        public async Task<object?> GetProductDetailsAsync(string sku)
        {
            var sql = @"SELECT 
                            p.Name, p.EAN, p.ProducerName, p.Category, p.DefaultImage,
                            i.Qty, i.Unit, i.ShippingCost,
                            pr.PricePerLogisticUnit
                        FROM Products p
                        LEFT JOIN Inventory i ON p.SKU = i.SKU
                        LEFT JOIN Prices pr ON p.SKU = pr.SKU
                        WHERE p.SKU = @sku
                        ";

            return await _db.QueryFirstOrDefaultAsync(sql, new { sku });
        }
    }
}
