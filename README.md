---

## name: README.md

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

* .NET 6 SDK or later
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
3. **Database migrations**

   ```bash
   cd Infrastructure.SqlServer
   dotnet ef database update
   ```
4. **Run the web project**

   ```bash
   dotnet run --project WebUI/WebUI.csproj
   ```

> *Note: Replace `WebUI/WebUI.csproj` with your front-end project path if different.*

## Configuration

Add a `.env` or `appsettings.json` entry for:

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
