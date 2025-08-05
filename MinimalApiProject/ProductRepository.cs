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
                        IF OBJECT_ID('Products', 'U') IS NOT NULL
                            DROP TABLE Products;
                        
                        CREATE TABLE Products (
                            Id INT PRIMARY KEY,
                            SKU NVARCHAR(100) NOT NULL,
                            Name NVARCHAR(500),
                            EAN NVARCHAR(100),
                            ProducerName NVARCHAR(500),
                            Category NVARCHAR(1000),
                            DefaultImage NVARCHAR(500)
                        );

                        
                        IF OBJECT_ID('Inventory', 'U') IS NOT NULL
                            DROP TABLE Inventory;

                        CREATE TABLE Inventory (
                            Id INT IDENTITY PRIMARY KEY,
                            ProductId INT,
                            SKU NVARCHAR(100),
                            Unit NVARCHAR(50),
                            Qty DECIMAL(18,3),
                            ShippingCost DECIMAL(18,2)
                        );
                        
                        IF OBJECT_ID('Prices', 'U') IS NOT NULL
                            DROP TABLE Prices;
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

            // Load Inventory
            var inventoryList = new List<InventoryCsv>();
            using (var reader2 = new StreamReader(inventoryFile))
            using (var csv2 = new CsvReader(reader2, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = ",",
                HeaderValidated = null,
                MissingFieldFound = null,
                BadDataFound = null
            }))
            {
                var records2 = csv2.GetRecords<InventoryCsv>().Where(x =>
                    int.TryParse(x.shipping?.Trim().TrimEnd('h'), out var h2) && h2 <= 24 && productMap.ContainsKey(x.sku));

                foreach (var r in records2)
                {
                    inventoryList.Add(r);
                }
            }

            BulkInsert(inventoryList.Select(r => new
            {
                ProductId = productMap[r.sku],
                r.sku,
                r.unit,
                Qty = decimal.TryParse(r.qty, CultureInfo.InvariantCulture, out var q) ? q : 0,
                ShippingCost = decimal.TryParse(r.shipping_cost, CultureInfo.InvariantCulture, out var sc) ? sc : 0
            }), "Inventory");

            // Load Prices
            var priceList = new List<PriceCsv>();
            using (var reader3 = new StreamReader(pricesFile))
            using (var csv3 = new CsvReader(reader3, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = "\",",
                HasHeaderRecord = false,
                HeaderValidated = null,
                MissingFieldFound = null,
                BadDataFound = null
            }))
            {
                priceList = csv3.GetRecords<PriceCsv>().ToList();
            }

            BulkInsert(priceList.Select(r => new
            {
                SKU = r.Column2,
                PricePerUnit = decimal.TryParse(r.Column3, CultureInfo.InvariantCulture, out var pu) ? pu : 0,
                PricePerLogisticUnit = decimal.TryParse(r.Column6, CultureInfo.InvariantCulture, out var plu) ? plu : 0
            }), "Prices");
        }
        private void BulkInsert<T>(IEnumerable<T> data, string tableName)
        {
            if (_db is SqlConnection sqlConn)
            {
                if (sqlConn.State != ConnectionState.Open)
                    sqlConn.Open();
                using var bulkCopy = new SqlBulkCopy(sqlConn);
                var dataTable = new DataTable();
                var props = typeof(T).GetProperties();

                foreach (var prop in props)
                {
                    dataTable.Columns.Add(prop.Name, Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType);
                }

                foreach (var item in data)
                {
                    var values = props.Select(p => p.GetValue(item) ?? DBNull.Value).ToArray();
                    dataTable.Rows.Add(values);
                }

                bulkCopy.DestinationTableName = tableName;
                if (tableName == "Inventory")
                {
                    bulkCopy.ColumnMappings.Add("ProductId", "ProductId");
                    bulkCopy.ColumnMappings.Add("SKU", "SKU");
                    bulkCopy.ColumnMappings.Add("Unit", "Unit");
                    bulkCopy.ColumnMappings.Add("Qty", "Qty");
                    bulkCopy.ColumnMappings.Add("ShippingCost", "ShippingCost");
                }
                if (tableName == "Prices")
                {
                    bulkCopy.ColumnMappings.Add("SKU", "SKU");
                    bulkCopy.ColumnMappings.Add("PricePerUnit", "PricePerUnit");
                    bulkCopy.ColumnMappings.Add("PricePerLogisticUnit", "PricePerLogisticUnit");
                }
                bulkCopy.WriteToServer(dataTable);
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
