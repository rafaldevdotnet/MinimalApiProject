# 🧩 Product API Project

Minimalistyczne REST API oparte na ASP.NET Core (.NET 7) z wykorzystaniem Dappera oraz SqlBulkCopy do szybkiego importu danych z plików CSV.

---

## 🚀 Funkcje

- Import danych z plików CSV:
  - `Products.csv`
  - `Inventory.csv`
  - `Prices.csv`
- Obsługa bazy danych:
  - Microsoft SQL Server (MSSQL)
- Wsparcie dla `SqlBulkCopy` (dla MSSQL) — szybki import dużych zbiorów danych
- Filtrowanie rekordów (np. `shipping <= 24h`, `available == 1`)
- Endpoint do pobierania szczegółów produktu
- Obsługa **szyfrowania poufnych danych w `appsettings.json`**

---

## 🔧 Wymagania

- .NET 7 SDK
- MS SQL Server
- Visual Studio / Rider / VS Code

---

## 🏗️ Instalacja

1. **Sklonuj repozytorium:**

   ```bash
   git clone https://github.com/rafaldevdotnet/MinimalApiProject.git   
   ```

2. **Skonfiguruj plik `appsettings.json`:**

   ```json
   {
     "ConnectionStrings": {
       "Default": "*Server=(localdb)\\MSSQLLocalDB;Database=LOCAL_DB;Trusted_Connection=True;"
     },
     "CsvDownloadUrls": {
       "Products": "https://adres/products.csv",
       "Inventory": "https://adres/inventory.csv",
       "Prices": "https://adres/prices.csv"
     }
   }
   ```

3. **Uruchom aplikację:**

   ```bash
   dotnet run
   ```

4. **Testuj przez Swaggera:**

   Po uruchomieniu aplikacji wejdź w przeglądarce pod `https://localhost:{PORT}/swagger`.

---

## 🔐 Szyfrowanie `appsettings.json`

Jeśli w `appsettings.json` wartość zaczyna się od `*`, aplikacja:

- zaszyfruje wartość,
- nadpisze ją w pliku (usuwając `*`),
- a następnie będzie odszyfrowywać ją przy użyciu w kodzie.

**Przykład:**

```json
"ConnectionStrings": {
  "Default": "*Server=..."
}
```

## 📦 Endpointy

| Metoda | Endpoint         | Opis                              |
|--------|------------------|-----------------------------------|
| POST   | `/import-data`   | Importuje dane z CSV do bazy     |
| GET    | `/product/{sku}` | Zwraca dane produktu             |

---

## 📁 Struktura projektu

```
ProductApiProject/
├── Program.cs
├── ProductRepository.cs
├── Models/
│   ├── ProductCsv.cs
│   ├── InventoryCsv.cs
│   └── PriceCsv.cs
└── appsettings.json
```

## 📄 Licencja

Projekt dostępny na licencji MIT.
