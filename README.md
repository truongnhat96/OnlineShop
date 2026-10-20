---
# OnlineShop

A demonstration of an e-commerce backend and foundational architecture built with ASP.NET Core MVC, Razor, Entity Framework Core, and Clean Architecture principles, including caching and comprehensive unit/integration tests. The solution illustrates separation of concerns across domain, application, and infrastructure layers, backed by SQL Server.

## Table of Contents

* [Features](#features)
* [Project Structure](#project-structure)
* [Prerequisites](#prerequisites)
* [Setup & Run](#setup--run)
* [Configuration](#configuration)
* [Technology Stack](#technology-stack)
* [Testing](#testing)
* [Contributing](#contributing)
* [License](#license)

## Features

**User Operations**

* Browse paginated products list
* Search products by name or category
* View product details with images and specifications
* Post comments and rate products
* Add, update, and remove items in a shopping cart
* Checkout using VNPAY and MOMO integrations
* View order history and status updates

**Administrator Operations**

* Create, update, and delete products
* Manage customer orders and update order statuses
* Write, edit, and publish blog posts
* Full CRUD for blog content

## Project Structure

```
OnlineShop.sln           # Solution file

/Entities                # Domain entities and value objects
/UseCase                  # Application services (business logic interfaces)
/UseCase.Caching          # Caching decorators for use cases
/UseCase.Tests            # Unit tests for application logic
/Infrastructure           # Core abstractions for data access
/Infrastructure.SqlServer # SQL Server implementation (EF Core)
/Infrastructure.SqlServer.Tests # Integration tests for data layer
```

## Prerequisites

* .NET 9 SDK
* SQL Server (LocalDB or full instance)
* Visual Studio 2022 / VS Code
* (Optional) Docker, if running in containers

## Setup & Run

1. **Clone the repository**

   ```bash
   git clone https://github.com/truongnhat96/OnlineShop.git
   cd OnlineShop
   ```
2. **Restore NuGet packages & build**

   ```bash
   dotnet restore
   dotnet build
   ```
3. **Use the existing database**

   Keep the existing SQL Server connection and OnlineShop database. This UI update adds no migrations, seed replacements or business-data changes.
4. **Run the web project**

   ```bash
   dotnet dev-certs https --trust
   dotnet run --project Infrastructure/Infrastructure.csproj --launch-profile https
   ```

Open **https://localhost:7042** for the real application. HTTPS is the default project launch profile; http://localhost:5085 redirects to HTTPS while this profile is running. Products, accounts, login, cart and admin use the existing SQL Server database and application services.

Trust the ASP.NET Core development certificate once per machine/user and accept the Windows certificate prompt if shown. In Visual Studio, select the **https** launch profile. Use `localhost` in the URL so it matches the development certificate.

The project under `tests/MotoShop.UiHarness` is only a UI automation fixture. It does not use real accounts or database data. It requires the explicit `--ui-fixtures` flag and runs on port **15187**. Do not use this host to operate the shop.

See [the runtime and UI test guide](docs/ui-review/README.md).

## Configuration

Summernote in article content and product descriptions supports automatic Cloudinary image uploads. Set `Cloudinary:CloudName`, `Cloudinary:ApiKey`, and `Cloudinary:ApiSecret` through ASP.NET configuration, or supply missing values in the repository-root `.env` as `Cloudinary__CloudName`, `Cloudinary__ApiKey`, `Cloudinary__ApiSecret`, then restart the app. Non-empty ASP.NET values take precedence per field. Product thumbnails and article covers still save locally. Empty defaults keep text editing available; image uploads report a configuration error until credentials are supplied. See [Cloudinary setup](docs/ke-hoach-tich-hop-cloudinary-summernote.md) and [configuration fix verification](docs/ui-review/cloudinary/config-fix/README.md).

The customer AI chat uses Google Gemini Developer API. Create a key at [Google AI Studio](https://aistudio.google.com/api-keys), add `GEMINI_API_KEY=your_key` to the existing repository-root `.env`, then restart the app. The default model is `gemini-3.5-flash-lite`; use a Free Tier project and check its current quota. No billing is enabled by this integration. For chat, only `GEMINI_API_KEY` is read from `.env`; explicit ASP.NET configuration takes precedence. Cloudinary reads its own credential keys separately. The old `HF_TOKEN` is no longer used. Keys stay on the server; AHP recommendations still operate independently. All service failures show one generic message; classified technical details go to logs. See [Gemini setup](docs/ke-hoach-tich-hop-gemini-chatbot.md) and [verification](docs/ui-review/chat/gemini/README.md).

Use `appsettings.json`, user-secrets or exported environment variables for other configuration:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.;Database=OnlineShop;Trusted_Connection=True;"
  },
  "VNPAY": {
    "Url": "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html",
    "TmnCode": "YourVnpayTerminal",
    "HashSecret": "YourVnpayHashSecret"
  },
  "MOMO": {
    "Endpoint": "https://test-payment.momo.vn/gw_payment/transactionProcessor",
    "PartnerCode": "YourMomoPartnerCode",
    "AccessKey": "YourMomoAccessKey",
    "SecretKey": "YourMomoSecretKey"
  }
}
```

## Technology Stack

* **Framework:** .NET 6+ (ASP.NET Core MVC, Razor views)  ([github.com](https://github.com/truongnhat96/OnlineShop))
* **ORM:** Entity Framework Core with SQL Server provider  ([github.com](https://github.com/truongnhat96/OnlineShop))
* **Architecture:** Clean Architecture (Onion/Clean layers)  ([github.com](https://github.com/truongnhat96/OnlineShop))
* **Caching:** In-memory and distributed caching via `UseCase.Caching` layer
* **Testing:** xUnit for unit tests and integration tests
* **Tooling:** EF Core Migrations, Dependency Injection, Settings configuration

## Testing

Run all tests using:

```bash
dotnet test
```

* **Application Logic Tests:** `UseCase.Tests` project
* **Infrastructure Tests:** `Infrastructure.SqlServer.Tests` project

## Contributing

Contributions are welcome:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/xyz`)
3. Commit changes (`git commit -m "Add xyz feature"`)
4. Push to branch (`git push origin feature/xyz`)
5. Open a Pull Request

Please ensure code style consistency and passing tests.

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE) for details.

© 2025 Lương Nhật Trường. All rights reserved.
