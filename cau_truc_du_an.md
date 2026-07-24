# Cấu trúc dự án C#

Dưới đây là cấu trúc hiện tại của dự án (đã bỏ qua các thư mục `bin`, `obj`, `.git`):

```text
.
├── Application
│   ├── Application.csproj
│   └── Class1.cs
├── Core
│   ├── Class1.cs
│   ├── Core.csproj
│   └── Interfaces
│       └── IRepository.cs
├── Infrastructure
│   ├── Class1.cs
│   ├── DataContext.cs
│   ├── Infrastructure.csproj
│   └── Repository.cs
├── Project.slnx
└── WebApi
    ├── appsettings.Development.json
    ├── appsettings.json
    ├── Controllers
    │   └── WeatherForecastController.cs
    ├── Program.cs
    ├── Properties
    │   └── launchSettings.json
    ├── WeatherForecast.cs
    ├── WebApi.csproj
    └── WebApi.http
```

## Sự phụ thuộc (Project References)

Các dự án (projects) trong giải pháp này được liên kết với nhau theo nguyên tắc của mô hình Clean Architecture:

- **WebApi** (Tầng trình diễn/API): Phụ thuộc vào **Application** và **Infrastructure**.
- **Infrastructure** (Tầng cơ sở hạ tầng/Data): Phụ thuộc vào **Core**.
- **Application** (Tầng ứng dụng/Business Logic): Phụ thuộc vào **Core**.
- **Core** (Tầng lõi/Domain): Độc lập, không phụ thuộc vào bất kỳ project nào khác trong solution.
