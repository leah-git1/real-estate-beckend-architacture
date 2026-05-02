# Repository Instructions

## What This Project Is
ASP.NET Core 8 Web API for a real estate platform. Users can browse/list properties, place rental orders, submit inquiries, and admins can manage everything. The frontend is a separate Angular app at `http://localhost:4200`.

---

## Solution Layout

```
WebApiShop.sln
├── Entities/        → EF Core domain models (auto-generated, #nullable disable)
├── DTOs/            → C# records for API input/output — never expose Entities directly
├── Repository/      → ShopContext (DbContext) + one repository class per entity
├── Services/        → Business logic + AutoMapper profile (AutoMapping.cs)
├── WebApiShop/      → Controllers, Middleware, Program.cs, nlog.config
└── TestProject/     → xUnit unit + integration tests
```

**Key files to know before touching anything:**
- `Repository/ShopContext.cs` — all DbSets and EF model config
- `Services/AutoMapping.cs` — every AutoMapper mapping lives here
- `WebApiShop/Program.cs` — all DI registrations and middleware order

---

## Tech Stack
- **Framework:** ASP.NET Core 8.0
- **ORM:** Entity Framework Core 8.0.24 (SQL Server in prod, InMemory in tests)
- **Mapping:** AutoMapper 12 — mappings in `Services/AutoMapping.cs`
- **Logging:** NLog via `WebApiShop/nlog.config` (file + email on Error)
- **Password strength:** zxcvbn-core — `IPasswordService.checkStrengthPassword()` returns `CheckPassword { password, strength }` where `strength` is 0–4; minimum required is `>= 2`
- **Tests:** xUnit + Moq + `Microsoft.EntityFrameworkCore.InMemory`

---

## How to Add a New Feature (exact order)

1. **Entity** → `Entities/MyEntity.cs` — add `DbSet<MyEntity>` to `ShopContext`, configure in `OnModelCreating`
2. **DTOs** → `DTOs/MyEntityDTO.cs`, `MyEntityCreateDTO.cs` etc. — use C# `record` types
3. **Mappings** → add `CreateMap<>` pairs in `Services/AutoMapping.cs`
4. **Repository interface** → `Repository/IMyEntityRepository.cs`
5. **Repository class** → `Repository/MyEntityRepository.cs` — inject `ShopContext`
6. **Service interface** → `Services/IMyEntityService.cs`
7. **Service class** → `Services/MyEntityService.cs` — inject `IMyEntityRepository` + `IMapper`
8. **Register** → add both as `.AddScoped<>` in `WebApiShop/Program.cs`
9. **Controller** → `WebApiShop/Controllers/MyEntityController.cs` with `[Route("api/[controller]")]`

---

## Strict Conventions

### Naming
| Thing | Pattern | Example |
|---|---|---|
| Entity | Singular PascalCase | `Product`, `Order` |
| DTO | `*DTO` / `*CreateDTO` / `*UpdateDTO` | `ProductCreateDTO` |
| Repository | `IProductRepository` / `ProductRepository` | — |
| Service | `IProductService` / `ProductService` | — |
| Controller route | lowercase | `api/product`, `api/order` |

### DTOs are C# records
```csharp
public record MyEntityCreateDTO(string Name, decimal Price);
```
`PageResponseDTO<T>` is a class (not record) — used for paginated list responses.

### Controller pattern
```csharp
[HttpGet("{id}")]
public async Task<ActionResult<MyDTO>> GetById(int id)
{
    var result = await _service.GetById(id);
    if (result == null) return NotFound();
    return Ok(result);
}

[HttpPost]
public async Task<ActionResult<MyDTO>> Create(MyCreateDTO dto)
{
    try {
        var result = await _service.Create(dto);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }
    catch (Exception ex) {
        return BadRequest(new { message = ex.Message });
    }
}
```
- Always use `ILogger<T>` — log warnings on NotFound, errors on exceptions
- `NoContent()` for successful deletes and updates
- Never return raw `Entities` — always map to DTOs first

### Service pattern
- All methods are `async Task<T>`
- Throw `Exception` with a message for business rule violations (e.g. weak password, duplicate email)
- Return `null` when a resource is not found (controller handles the 404)

### Repository pattern
- Inject `ShopContext` directly
- Use `.Include()` for navigation properties when needed
- Use `FirstOrDefaultAsync`, `ToListAsync`, `FindAsync`

---

## Critical Entity Rules

| Entity | Required fields | Notes |
|---|---|---|
| `Product` | `Title`, `TransactionType` | `TransactionType` must be `"Rent"` or `"Sale"` / `"מכירה"`. Sale items cannot be booked. |
| `User` | `FullName`, `Email`, `Password` | `Email` is unique in DB |
| `Order` | `UserId`, `TotalAmount` | `TotalAmount` is `decimal`, `OrderDate` is `DateTime?` |
| `OrderItem` | `OrderId`, `ProductId`, `PriceAtPurchase` | `StartDate`/`EndDate` are `DateTime?` |
| `PropertyInquiry` | `Name`, `Phone`, `Email`, `ProductId`, `UserId`, `OwnerId` | Default status: `"New"` |
| `AdminInquiry` | `Name`, `Email`, `Phone`, `Subject` | `UserId` is nullable |

---

## Middleware Pipeline (order is fixed — do not change)
1. `UseErrorHandling` — catches all unhandled exceptions → 500 JSON
2. `AdminAuthorizationMiddleware` — blocks `/api/admin/*` unless header `IsAdmin: true` is present. Exception: `POST /api/admin/inquiry` is public.
3. `UseRating` — logs every request to the `Ratings` table
4. `UseStaticFiles` → `UseRouting` → `UseAuthorization` → `MapControllers`

---

## Testing Rules

- **Always use `DatabaseFixture`** for integration tests — it uses `UseInMemoryDatabase(Guid.NewGuid().ToString())`, never SQL Server
- **Unit tests:** mock `IRepository` and `IMapper` with Moq; test service logic only
- When creating a `Product` in tests, always set `TransactionType = "Rent"` (it is required)
- `RegisterUser` **throws** `Exception` on weak password — test with `Assert.ThrowsAsync<Exception>(...)`
- `DeleteUser` internally calls `GetProductsByOwnerId` — mock it to return `new List<Product>()` in unit tests
- `UserProfileDTO` constructor: `(int UserId, string FullName, string Phone, string Address, bool IsAdmin)` — note: no Email

```powershell
dotnet test TestProject/TestProject.csproj
```

---

## Build

```powershell
dotnet restore
dotnet build        # expect ~51 nullable warnings (CS8603 etc.) — these are pre-existing, not errors
dotnet run --project WebApiShop
# API: http://localhost:5202
# Swagger: http://localhost:5202/swagger  (dev only)
```

**Do not use `tail`, `grep`, or other Unix commands** — this project runs on Windows. Use `findstr` or PowerShell equivalents.

---

## Known Quirks
- `ProductRepository.GetProducts` calls `Console.WriteLine(query.ToQueryString())` — this is intentional debug output, leave it
- `ProductRepository.UpdateProduct` has a duplicate `IsAvailable` assignment block with a `Console.WriteLine` — pre-existing, do not remove
- Entities are in `#nullable disable` mode (auto-generated by EF Core Power Tools) — do not add nullable annotations to them
- `ICategoriesServies` has a typo (missing 'r') — this is the existing interface name, match it exactly when referencing
- `PageResponseDTO<T>` is a class with property setters, not a record — instantiate with `new PageResponseDTO<T>()` then set properties
