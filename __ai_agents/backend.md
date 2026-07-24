# Backend Context

## Architecture
This project follows a **Clean Architecture** pattern using C# and .NET 10. The solution (`Project.slnx`) is divided into the following layers:

1. **Core**: The domain layer. Contains entities and core interfaces (e.g., `IRepository`). *No dependencies.*
2. **Application**: The business logic layer. Contains use cases and application services (e.g., `IService`, `Service`). *Depends on Core.*
3. **Infrastructure**: The data access layer. Contains Entity Framework Core implementations (e.g., `DataContext`, `Repository`). *Depends on Core.*
4. **WebApi**: The presentation layer (ASP.NET Core API). Contains Controllers and DI setup. *Depends on Application and Infrastructure.*

## Tech Stack & Patterns
- **Language**: C#
- **Framework**: .NET 10 (ASP.NET Core)
- **Database/ORM**: Entity Framework Core
- **Patterns**: Dependency Injection, Repository Pattern, Generic Services.
- **Integration**: The WebApi is configured to serve the frontend SPA using `UseStaticFiles()` and `MapFallbackToFile("index.html")`.
