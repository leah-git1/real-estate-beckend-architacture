# Real Estate Backend API

ASP.NET Core Web API for a real estate platform. Enables property listings, user registration, orders, inquiries, and admin management.

---

## Table of Contents

1. [Tech Stack](#tech-stack)
2. [Project Structure](#project-structure)
3. [Setup and Installation](#setup-and-installation)
4. [Configuration](#configuration)
5. [Architecture](#architecture)
6. [API Reference](#api-reference)
7. [Data Models](#data-models)
8. [Authentication and Authorization](#authentication-and-authorization)
9. [Business Flows](#business-flows)
10. [Testing](#testing)
11. [Contributing](#contributing)
12. [Security Considerations](#security-considerations)
13. [Troubleshooting](#troubleshooting)

---

## Tech Stack

| Component | Technology |
|-----------|------------|
| Framework | ASP.NET Core 8.0 |
| Database | SQL Server |
| ORM | Entity Framework Core 8 |
| Logging | NLog (file + email) |
| Email | MailKit (Gmail SMTP) |
| Mapping | AutoMapper |
| Auth | JWT (HttpOnly cookie) |
| API Docs | Swagger (Development only) |

---

## Project Structure

```
Real-Estate-Backend/
├── WebApiShop/                    # Main API project
│   ├── Controllers/
│   │   ├── UsersController.cs
│   │   ├── ProductController.cs
│   │   ├── OrderController.cs
│   │   ├── CategoryController.cs
│   │   ├── AdminController.cs
│   │   ├── PasswordController.cs
│   │   ├── ProductImageController.cs
│   │   └── PropertyInquiryController.cs
│   ├── Middleware/
│   │   ├── ErrorHandlingMiddleware.cs
│   │   ├── AdminAuthorizationMiddleware.cs
│   │   └── RatingMiddleware.cs
│   ├── Properties/
│   │   └── launchSettings.json
│   ├── wwwroot/
│   │   └── images/               # Uploaded product images
│   ├── Program.cs
│   ├── appsettings.json
│   ├── appsettings.Development.json
│   ├── nlog.config
│   └── SCRIPT.txt                # SQL script to create the database
├── Entities/                      # Domain models (EF Core Power Tools generated)
├── Repository/                    # Data access layer (EF Core, DbContext)
├── Services/                      # Business logic layer
├── DTOs/                          # Data Transfer Objects
└── TestProject/                   # Unit & integration tests
```

---

## Setup and Installation

### Prerequisites

| Requirement | Version / Details |
|-------------|-------------------|
| .NET SDK | 8.0 or later |
| SQL Server | LocalDB, Express, or full instance |
| IDE (optional) | Visual Studio 2022, Rider, or VS Code with C# extension |

Verify .NET:

```powershell
dotnet --version
```

### 1. Restore Dependencies

```powershell
dotnet restore WebApiShop.sln
```

### 2. Create the Database

**Option A: Run the SQL Script (recommended)**

1. Open SQL Server Management Studio (SSMS) or run `sqlcmd`
2. Execute the script at `WebApiShop/SCRIPT.txt`
3. Creates `RealEstateDB_` and all required tables

**Option B: EF Core Migrations**

```powershell
dotnet ef migrations add InitialCreate --project Repository --startup-project WebApiShop
dotnet ef database update --project Repository --startup-project WebApiShop
```

### 3. Configure Connection String

Edit `WebApiShop/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_SERVER;Database=RealEstateDB_;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```

Also update the hardcoded connection string in `Program.cs` (line 31) to use the config:

```csharp
builder.Services.AddDbContext<ShopContext>(option =>
    option.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
```

### 4. Build and Run

```powershell
dotnet build
dotnet run --project WebApiShop
```

**Launch profiles:**
- HTTP: `http://localhost:5202`
- HTTPS: `https://localhost:7046`
- Swagger: `http://localhost:5202/swagger` (Development only)

### Quick Commands

| Command | Purpose |
|---------|---------|
| `dotnet restore` | Restore packages |
| `dotnet build` | Build solution |
| `dotnet run --project WebApiShop` | Run API |
| `dotnet watch run --project WebApiShop` | Run with hot reload |
| `dotnet test TestProject/TestProject.csproj` | Run tests |

---

## Configuration

### appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_SERVER;Database=RealEstateDB_;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "EmailSettings": {
    "SmtpServer": "smtp.gmail.com",
    "SmtpPort": "587",
    "SenderEmail": "your-email@gmail.com",
    "SenderPassword": "your-app-password",
    "RecipientEmail": "recipient@example.com"
  }
}
```

> **Warning:** Never commit real credentials to source control. Use User Secrets or environment variables.

### User Secrets (recommended for dev)

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=...;Database=...;" --project WebApiShop
dotnet user-secrets set "EmailSettings:SenderPassword" "app-password" --project WebApiShop
```

### nlog.config

- File logging: `../../../logFile.log`
- Email logging: Sends on Error level (configure SMTP in `nlog.config`)

### CORS (Program.cs)

- Allowed origin: `http://localhost:4200` (Angular frontend)
- Exposed header: `IsAdmin`

---

## Architecture

### High-Level Flow

```
HTTP Request
    ↓
Middleware (ErrorHandling → AdminAuthorization → Rating → StaticFiles → Routing → Authorization)
    ↓
Controllers
    ↓
Services (business logic)
    ↓
Repositories (data access)
    ↓
Database (SQL Server)
```

### Project Dependencies

```
WebApiShop → Services
Services   → Repository, DTOs
Repository → Entities, DTOs
TestProject → DTOs, Entities, Repository, Services
```

### Middleware Pipeline (order)

1. `UseHttpsRedirection`
2. `UseCors`
3. `UseErrorHandling` – Catches unhandled exceptions, returns 500
4. `AdminAuthorizationMiddleware` – Blocks non-admin access to `/api/admin/*` (except POST `/api/admin/inquiry`)
5. `UseRating` – Logs every request to the Ratings table
6. `UseStaticFiles`, `UseRouting`, `UseCors`, `UseAuthorization`, `MapControllers`

### Key Patterns

- **Repository pattern** – Abstracts data access
- **Service pattern** – Business logic in services
- **DTO pattern** – Entities never exposed directly
- **AutoMapper** – Mappings defined in `Services/AutoMapping.cs`
- **Dependency Injection** – All services/repos registered as Scoped in `Program.cs`

---

## API Reference

**Base URL:** `/api`
**Content-Type:** `application/json`
**Auth:** JWT stored in HttpOnly cookie (`jwt`), set on login/register
**Admin routes:** Require `IsAdmin: true` header AND valid JWT with Admin role (except POST `/api/admin/inquiry`)
**CORS:** `http://localhost:4200`

---

### Users (`/api/users`)

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/` | Admin | Get all users |
| GET | `/{id}` | Authorized | Get user by ID |
| POST | `/` | Public | Register |
| POST | `/login` | Public | Login |
| POST | `/logout` | Authorized | Logout (clears JWT cookie) |
| PUT | `/{id}` | Authorized | Update user |
| DELETE | `/{id}` | Admin | Delete user |

**Register / Login response:** Sets `jwt` HttpOnly cookie, returns `UserProfileDTO`

---

### Products (`/api/product`)

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/` | Public | List products (filtered, paged) |
| GET | `/{id}` | Public | Get by ID |
| POST | `/` | Authorized | Create product |
| PUT | `/{id}` | Authorized | Update product |
| DELETE | `/{id}` | Authorized | Delete product |
| GET | `/owner/{ownerId}` | Public | Get by owner |
| GET | `/check-availability` | Public | Check availability |
| GET | `/search?query=` | Public | Search products |
| GET | `/featured?count=5` | Public | Featured products |

**List query params:** `categoryIds`, `title`, `city`, `minPrice`, `maxPrice`, `rooms`, `beds`, `position`, `skip`

**check-availability query params:** `productId`, `start` (DateTime), `end` (DateTime)

---

### Orders (`/api/order`)

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/` | Public | Get all orders |
| GET | `/{id}` | Public | Get by ID |
| GET | `/user/{userId}` | Public | Get by user |
| POST | `/` | Public | Create order |
| PUT | `/{orderId}/status` | Public | Update status |
| PUT | `/{orderId}/delivered` | Public | Mark delivered |
| GET | `/occupied-dates/{productId}` | Public | Occupied dates for month/year |

**Create body:** `UserId`, `OrderItems[]` (ProductId, StartDate?, EndDate?)

**occupied-dates query params:** `month`, `year`

---

### Categories (`/api/category`)

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/` | Public | Get all |
| GET | `/{id}` | Public | Get by ID |
| POST | `/` | Public | Create |
| PUT | `/{id}` | Public | Update |
| DELETE | `/{id}` | Public | Delete |

---

### Product Images (`/api/productimage`)

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/{id}` | Public | Get by ID |
| GET | `/productImage/{productId}` | Public | Get by product |
| POST | `/` | Public | Add image (URL) |
| POST | `/upload` | Public | Upload file (multipart/form-data) |
| PUT | `/{imageId}` | Public | Update |
| DELETE | `/{id}` | Public | Delete |

---

### Property Inquiries (`/api/propertyinquiry`)

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/` | Public | Get all |
| GET | `/{id}` | Public | Get by ID |
| GET | `/owner/{ownerId}` | Public | Get by owner |
| GET | `/user/{userId}` | Public | Get by user |
| POST | `/` | Public | Create |
| PUT | `/{id}/status` | Public | Update status |
| DELETE | `/{id}` | Public | Delete |

**Create body:** `ProductId`, `UserId`, `OwnerId`, `Name`, `Phone`, `Email`, `Message`

---

### Admin (`/api/admin`)

**All routes require `IsAdmin: true` header + Admin JWT role, except POST `/inquiry`.**

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/users` | Get all users |
| GET | `/products` | Get all products |
| GET | `/orders` | Get all orders |
| GET | `/statistics` | Admin statistics |
| GET | `/inquiries` | Get all admin inquiries |
| GET | `/inquiry/{id}` | Get inquiry by ID |
| POST | `/inquiry` | Create inquiry (public) |
| PUT | `/inquiry/{id}/status` | Update inquiry status |
| DELETE | `/user/{id}` | Delete user |
| DELETE | `/product/{id}` | Delete product |
| DELETE | `/order/{id}` | Delete order |
| DELETE | `/inquiry/{id}` | Delete inquiry |

---

### Password (`/api/password`)

| Method | Route | Description |
|--------|-------|-------------|
| POST | `/` | Check password strength (body: raw string) |

**Response:** `CheckPassword` (password, strength 0–4)

---

### Error Responses

| Status | When |
|--------|------|
| 400 | Validation or bad request |
| 401 | Unauthorized (missing/invalid JWT) |
| 403 | Admin route without required header/role |
| 404 | Resource not found |
| 409 | Order conflict (product unavailable) |
| 500 | Unhandled exception |

---

## Data Models

### Key Entities

**User:** `UserId`, `FullName`, `Email`, `Password`, `Phone`, `Address`, `IsAdmin`

**Product:** `ProductId`, `Title`, `Description`, `Price`, `ImageUrl`, `CategoryId`, `City`, `Rooms`, `Beds`, `OwnerId`, `IsAvailable`, `TransactionType`

**Order:** `OrderId`, `UserId`, `OrderDate`, `TotalAmount`, `Status`

**OrderItem:** `OrderItemId`, `OrderId`, `ProductId`, `StartDate`, `EndDate`, `PriceAtPurchase`

**Category:** `CategoryId`, `CategoryName`, `Description`

**PropertyInquiry:** `InquiryId`, `ProductId`, `UserId`, `OwnerId`, `Name`, `Phone`, `Email`, `Message`, `Status`

**AdminInquiry:** `InquiryId`, `UserId`, `Name`, `Email`, `Phone`, `Subject`, `Message`, `Status`

**Rating:** `RatingId`, `Host`, `Method`, `Path`, `Referer`, `UserAgent`, `RecordDate`

### Entity Relationships

```
User       ◄── Products (Owner), Orders
Product    ◄── ProductImages, OrderItems, PropertyInquiries
Category   ◄── Products
Order      ◄── OrderItems
OrderItem  ◄── Product
PropertyInquiry ◄── Product, User, Owner(User)
AdminInquiry    ◄── User (optional)
Rating (standalone - request log)
```

### DTOs

**User:** `UserProfileDTO`, `UserRegisterDTO`, `UserLoginDTO`, `UserUpdateDTO`, `AuthResultDTO`

**Product:** `ProductSummaryDTO`, `ProductDetailsDTO`, `ProductCreateDTO`, `ProductUpdateDTO`, `ProductViewDTO`, `PageResponseDTO<T>`

**Order:** `OrderDTO`, `OrderCreateDTO`, `OrderItemDTO`, `OrderStatusUpdateDTO`, `OccupiedDatesResponseDTO`, `OrderHistoryDTO`, `OrderHistoryAdminDTO`, `OrderAdminDTO`

**Category:** `CategoryDTO`, `CategoryCreateDTO`, `CategoryUpdateDTO`

**Images:** `ProductImageDTO`, `ProductImageCreateDTO`, `ProductImageUpdateDTO`, `ProductImageUrlDTO`

**Inquiries:** `PropertyInquiryDTO`, `PropertyInquiryCreateDTO`, `PropertyInquiryStatusUpdateDTO`, `AdminInquiryDTO`, `AdminInquiryCreateDTO`, `AdminInquiryStatusUpdateDTO`

**Admin:** `AdminStatisticsDTO`

All mappings are defined in `Services/AutoMapping.cs`.

---

## Authentication and Authorization

### Overview

Authentication uses JWT tokens stored in an **HttpOnly cookie** (`jwt`, 8-hour expiry). Admin access additionally requires the `IsAdmin: true` request header validated by `AdminAuthorizationMiddleware`.

### Registration Flow

1. POST `/api/users` with `UserRegisterDTO`
2. Password strength validated (zxcvbn score ≥ 2); email uniqueness checked
3. Returns `UserProfileDTO` and sets `jwt` HttpOnly cookie

### Login Flow

1. POST `/api/users/login` with `UserLoginDTO`
2. On success, sets `jwt` HttpOnly cookie and returns `UserProfileDTO`
3. On failure, returns 400

### Logout

POST `/api/users/logout` — deletes the `jwt` cookie.

### Password Update

PUT `/api/users/{id}` with `UserUpdateDTO`. If `Password` is provided, `OldPassword` is required and the new password must have strength ≥ 2.

### Admin Authorization

- Requests to `/api/admin/*` require the `IsAdmin: true` header
- POST `/api/admin/inquiry` is public (no header required)
- AdminController additionally uses `[Authorize(Roles = "Admin")]`

### Cookie Settings

```
HttpOnly: true
Secure: true
SameSite: Strict
Expires: +8 hours
```

---

## Business Flows

### Order Creation

1. POST `/api/order` with `UserId` and `OrderItems` (ProductId, StartDate, EndDate)
2. For each item with dates: availability is checked
3. If unavailable → 409 Conflict
4. For Sale-type products: `IsAvailable` is set to `false` after purchase
5. Total = sum of `PriceAtPurchase` per item; `OrderDate` = UTC now

### Availability Check

`GET /api/product/check-availability?productId=&start=&end=` returns `false` if:
- Product not found or unavailable
- TransactionType is "Sale"
- Invalid/missing dates
- Dates overlap with existing OrderItems

### Property Inquiry

1. POST `/api/propertyinquiry`
2. Status defaults to `"New"`; saved and returned as `PropertyInquiryDTO`

### Admin Inquiry (Contact Form)

1. POST `/api/admin/inquiry` (no auth required)
2. Saves `AdminInquiry`; sends email to configured `RecipientEmail`
3. Returns `AdminInquiryDTO`

### Image Upload

1. POST `/api/productimage/upload` with `multipart/form-data`
2. Saved to `wwwroot/images/{Guid}.ext`
3. Returns relative URL (e.g. `/images/xxx.jpg`)

### Occupied Dates

`GET /api/order/occupied-dates/{productId}?month=&year=` returns all booked dates for a product in the given month/year.

---

## Testing

### Stack

| Package | Purpose |
|---------|---------|
| xUnit 2.5.3 | Test framework |
| Moq 4.20.72 | Mocking |
| Moq.EntityFrameworkCore 8.0.1.7 | EF Core mocking |
| EF Core InMemory 8.0.24 | In-memory DB for integration tests |
| coverlet.collector 6.0.0 | Code coverage |

### Run Tests

```powershell
dotnet test TestProject/TestProject.csproj
dotnet test TestProject/TestProject.csproj --collect:"XPlat Code Coverage"
```

### DatabaseFixture

`DatabaseFixture` connects to a real SQL Server test database (`TestDataBase`) and calls `EnsureCreated()` / `EnsureDeleted()` for isolation. Implement `IClassFixture<DatabaseFixture>` in integration test classes.

> Note: Tests currently use a hardcoded SQL Server connection string. For fully isolated tests, consider switching to `UseInMemoryDatabase`.

### Unit Tests

Mock `I*Repository` and `IMapper`; test service logic in isolation.

### Integration Tests

Use real repositories against the `DatabaseFixture` SQL Server test database.

### Existing Tests

| File | Type |
|------|------|
| `UserUnitTest` | Unit |
| `OrdersUnitTest` | Unit |
| `CategoriesUnitTest` | Unit |
| `ProductUnitTest` | Unit |
| `UserIntegrationTest` | Integration |
| `OrderIntegrationTest` | Integration |
| `CategoriesIntegrationTest` | Integration |
| `ProductIntegrationTest` | Integration |

---

## Contributing

### Adding a New Feature

1. **Entity:** Create in `Entities/`, add `DbSet` to `ShopContext`, update `SCRIPT.txt`
2. **DTOs:** Create in `DTOs/`, add mappings in `Services/AutoMapping.cs`
3. **Repository:** `IMyEntityRepository` + `MyEntityRepository`, register in `Program.cs`
4. **Service:** `IMyEntityService` + `MyEntityService`, register in `Program.cs`
5. **Controller:** `MyEntityController` with `[Route("api/[controller]")]`
6. **Tests:** Unit and integration tests

### Naming Conventions

- Entity: singular (`Product`)
- DTO: `*DTO`, `*CreateDTO`, `*UpdateDTO`
- Repository: `IProductRepository`, `ProductRepository`
- Service: `IProductService`, `ProductService`
- Controller: `ProductController` → `api/product`

### Code Style

- Use `async Task` for all I/O operations
- Log with `ILogger` and structured messages
- Return `NotFound()` for missing resources
- No circular project references

### Checklist

- [ ] Tests added
- [ ] No secrets committed to source control
- [ ] `dotnet build` succeeds
- [ ] `dotnet test` passes

---

## Security Considerations

### Current State

- JWT auth via HttpOnly cookie (8hr expiry)
- Admin access: JWT role + `IsAdmin: true` header
- Passwords stored in plain text — **must be hashed**
- Credentials (email SMTP password) in `appsettings.json` — **move to User Secrets / env vars**
- No per-resource ownership checks (any authenticated user can update any product)
- CORS restricted to `http://localhost:4200`
- HTTPS supported
- SQL injection: EF Core parameterized queries

### Critical Risks

1. **Plain-text passwords** – Hash with bcrypt or ASP.NET Core Identity
2. **Credentials in appsettings** – Use User Secrets, env vars, or AWS Secrets Manager / Key Vault
3. **Hardcoded connection string in Program.cs** – Use `builder.Configuration.GetConnectionString("DefaultConnection")`
4. **No ownership checks** – Any authenticated user can edit/delete any product or order
5. **Admin header easily spoofable** – The `IsAdmin` header check is a secondary check; rely on JWT role claims as the primary enforcement

### Deployment Checklist

- [ ] Secrets externalized (no credentials in committed config files)
- [ ] HTTPS enforced
- [ ] CORS restricted to production frontend URL
- [ ] Passwords hashed (bcrypt / Argon2)
- [ ] Swagger disabled in production
- [ ] `dotnet list package --vulnerable` clean
- [ ] Connection string uses config, not hardcoded value

---

## Troubleshooting

| Issue | Solution |
|-------|----------|
| SQL connection failed | Verify SQL Server is running; fix `Server` value in connection string |
| Database does not exist | Run `WebApiShop/SCRIPT.txt` in SSMS |
| Port in use | Change URL in `launchSettings.json` |
| CORS errors | Ensure frontend runs at `http://localhost:4200` or update `Program.cs` |
| NLog file not found | Create log directory or fix path in `nlog.config` |
| MailKit errors | Check SMTP credentials in `appsettings.json`; disable email target if unused |
| JWT not sent | Ensure requests include credentials (cookies); check `SameSite` / `Secure` settings in dev |
| 403 on admin routes | Include `IsAdmin: true` header and ensure JWT has Admin role |

---

*Last updated: 2025*
