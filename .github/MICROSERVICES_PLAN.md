# Microservices Distribution Plan – Real Estate Backend

## Current State
Single ASP.NET Core 8 monolith with 7 domain modules sharing one SQL Server database via Entity Framework Core.

---

## Microservices Architecture Target

### Phase 1: Core Services (Immediate)

#### 1. **User Service** (Authentication & Profile Management)
- **Responsibilities**: User registration, login, profiles, admin role management
- **Entities**: User, CheckPassword
- **DTOs**: UserRegisterDTO, UserLoginDTO, UserProfileDTO, UserUpdateDTO, AuthResultDTO
- **Database**: SQL Server (dedicated)
- **API Contract**:
  - `POST /api/users/register` – Register new user
  - `POST /api/users/login` – Login (returns JWT)
  - `GET /api/users/{id}` – Get user profile
  - `PUT /api/users/{id}` – Update profile
  - `GET /api/users/{id}/is-admin` – Check admin status (internal)
- **Dependencies**: None (foundational)
- **Security**: Issues JWTs; validates bearer tokens
- **Persistence**: UsersRepository → ShopContext → User table

#### 2. **Property Service** (Listings & Management)
- **Responsibilities**: Product CRUD, property metadata, transaction types (Rent/Sale)
- **Entities**: Product, Category, ProductImage
- **DTOs**: ProductCreateDTO, ProductUpdateDTO, ProductDTO, ProductDetailDTO, CategoryDTO, ProductImageDTO
- **Database**: SQL Server (dedicated)
- **API Contract**:
  - `GET /api/properties` – List properties (paginated)
  - `GET /api/properties/{id}` – Get property detail
  - `POST /api/properties` – Create property (requires auth + ownership)
  - `PUT /api/properties/{id}` – Update property
  - `DELETE /api/properties/{id}` – Delete property
  - `GET /api/properties/{id}/occupied-dates` – Get booking calendar
  - `POST /api/categories` – Manage categories
- **Dependencies**: User Service (validates owner ID)
- **Persistence**: ProductRepository, ProductImageRepository, CategoryRepository

#### 3. **Booking Service** (Orders & Rentals)
- **Responsibilities**: Order creation, booking management, rental lifecycle
- **Entities**: Order, OrderItem
- **DTOs**: OrderCreateDTO, OrderDTO, OrderHistoryDTO, OrderStatusUpdateDTO
- **Database**: SQL Server (dedicated)
- **API Contract**:
  - `POST /api/bookings` – Create booking (validates property availability)
  - `GET /api/bookings/{id}` – Get booking details
  - `PUT /api/bookings/{id}/status` – Update booking status
  - `GET /api/bookings/user/{userId}` – Get user's bookings
  - `GET /api/bookings/property/{propertyId}` – Get property's bookings
- **Dependencies**: 
  - User Service (validates user/renter)
  - Property Service (validates property exists, checks if Rent-type)
- **Persistence**: OrderRepository, OrderItemRepository
- **Business Rules**:
  - Sale-type properties cannot be booked
  - Prevent double-booking (date range conflict detection)

#### 4. **Inquiry Service** (Lead & Message Management)
- **Responsibilities**: Property inquiries, admin inquiry handling
- **Entities**: PropertyInquiry, AdminInquiry
- **DTOs**: PropertyInquiryCreateDTO, PropertyInquiryDTO, AdminInquiryDTO, AdminInquiryStatusUpdateDTO
- **Database**: SQL Server (dedicated)
- **API Contract**:
  - `POST /api/inquiries/property` – Create property inquiry
  - `GET /api/inquiries/property/{id}` – Get inquiry
  - `PUT /api/inquiries/property/{id}/status` – Update status
  - `POST /api/inquiries/admin` – Admin-level inquiry (internal)
- **Dependencies**: User Service, Property Service
- **Persistence**: PropertyInquiryRepository, AdminInquiryRepository

#### 5. **Rating Service** (Reviews & Feedback)
- **Responsibilities**: User ratings, property reviews, feedback collection
- **Entities**: Rating
- **DTOs**: RatingDTO (to be defined)
- **Database**: SQL Server (dedicated)
- **API Contract**:
  - `POST /api/ratings` – Submit rating
  - `GET /api/ratings/property/{id}` – Get property ratings
  - `GET /api/ratings/average/{propertyId}` – Get average rating
- **Dependencies**: User Service, Booking Service (validates user booked property)
- **Persistence**: RatingRepository

#### 6. **Admin Service** (Oversight & Analytics)
- **Responsibilities**: Admin dashboard, statistics, system monitoring
- **Entities**: (queries other services)
- **DTOs**: AdminStatisticsDTO, OrderHistoryAdminDTO, OrderAdminDTO
- **Database**: Reads from all service DBs (eventually: aggregates via queues)
- **API Contract**:
  - `GET /api/admin/statistics` – Platform stats
  - `GET /api/admin/orders` – All orders (filtered)
  - `GET /api/admin/users` – All users (filtered)
- **Dependencies**: All services (read-only queries initially)
- **Security**: Requires `IsAdmin: true` claim; protected by AdminAuthorizationMiddleware
- **Persistence**: Distributed queries (later: event sourcing)

#### 7. **Notification Service** (Email & Logging)
- **Responsibilities**: Email notifications, event logging, request analytics
- **Entities**: (logs only – separate schema)
- **Services**: EmailService, NLog integration
- **Database**: SQL Server (dedicated – logs + email queue)
- **API Contract**:
  - `POST /api/notifications/send-email` (internal) – Enqueue email
  - Async workers process queue
- **Dependencies**: All services (subscribes to events)
- **Tech**: MailKit, NLog, background job scheduler (Hangfire)

---

## Phase 2: Cross-Cutting Concerns

### API Gateway Layer
```
Client
  ↓
API Gateway (routing, rate limiting, JWT validation)
  ├→ User Service (port 5202)
  ├→ Property Service (port 5203)
  ├→ Booking Service (port 5204)
  ├→ Inquiry Service (port 5205)
  ├→ Rating Service (port 5206)
  ├→ Admin Service (port 5207)
  └→ Notification Service (port 5208)
```

### Inter-Service Communication
- **Synchronous**: REST (HTTP) with retry logic + circuit breakers
- **Asynchronous**: Message queue (RabbitMQ or Azure Service Bus)
  - Order created → Email notification queued
  - Booking status updated → Property service notified
  - Property deleted → Cleanup bookings and inquiries

### Shared Patterns
- **DTO Models**: Defined in separate NuGet packages (e.g., `RealEstate.Contracts`)
- **JWT Secret**: Shared among all services (stored in Azure Key Vault)
- **Logging**: Centralized via Serilog → ELK stack
- **Error Handling**: Consistent error format across all APIs
- **Health Checks**: Each service exposes `/health` endpoint

---

## Phase 3: Data Migration Strategy

### Database Separation Timeline
| Service | Current DB | Phase 1 | Phase 2 | Phase 3 |
|---------|-----------|--------|--------|---------|
| User | ShopContext | Migrate to `RealEstate_Users` | Keep | Keep |
| Property | ShopContext | Migrate to `RealEstate_Properties` | Keep | Keep |
| Booking | ShopContext | Migrate to `RealEstate_Bookings` | Keep | Keep |
| Inquiry | ShopContext | Migrate to `RealEstate_Inquiry` | Keep | Keep |
| Rating | ShopContext | Migrate to `RealEstate_Ratings` | Keep | Keep |
| Admin | ShopContext | Read-only on all DBs | Event store | CQRS |
| Notification | ShopContext | Migrate to `RealEstate_Notifications` | Keep | Keep |

### Migration Approach
1. Create new database per service
2. Use EF Core migrations in each service project
3. Dual-write pattern during transition (write to both old + new DB)
4. Validate data consistency over 2-week period
5. Cutover traffic to new services
6. Decommission monolith

---

## Phase 4: Advanced Patterns (Post-MVP)

### Event Sourcing
- Every state change (order created, property updated, etc.) stored as event
- Event store: Single source of truth
- Services subscribe to events and update their read models

### CQRS (Command Query Responsibility Segregation)
- Commands: User actions (create booking, update profile)
- Queries: Read operations (list properties, get ratings)
- Separate DB schemas for write and read optimization

### Service Mesh (Istio)
- Automatic retries and circuit breakers
- Distributed tracing (Jaeger)
- mTLS between services

---

## Deployment & Infrastructure

### Containerization (Docker)
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY bin/Release/net8.0/publish .
ENTRYPOINT ["dotnet", "UserService.dll"]
```

### Kubernetes Orchestration
- Each service as separate deployment
- Horizontal pod autoscaling based on CPU/memory
- ConfigMaps for environment variables
- Secrets for DB connection strings and JWT keys

### CI/CD Pipeline
- Per-service build jobs (GitHub Actions)
- Unit tests + integration tests before merge
- Docker image pushed to registry on tag
- ArgoCD deploys to K8s cluster

---

## Fallback & Resilience

### Timeout & Retry Strategy
```csharp
// Polly library usage (pseudo-code)
var policy = Policy
    .Handle<HttpRequestException>()
    .WaitAndRetryAsync(retryCount: 3, sleepDurationProvider: x => TimeSpan.FromSeconds(x * 2))
    .WrapAsync(Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(5)));
```

### Compensation Transactions
- Booking created but payment fails → Rollback booking
- Multi-service transaction across User + Booking + Notification
- Use Saga pattern (orchestrator or choreography)

---

## Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|-----------|
| Network latency between services | Slow API response | Caching layer (Redis), async messaging |
| Service A down → cascading failure | System unavailable | Circuit breaker, fallback responses |
| Data consistency across DBs | Orphaned records | Event sourcing, compensating transactions |
| Complex testing | Bug escapes | Contract testing, integration test harness |
| Operational complexity | Support burden | Observability (logs, metrics, traces), runbooks |

---

## Success Metrics

- **Deployment frequency**: From weekly → daily
- **Mean time to recovery (MTTR)**: From 2 hours → 15 minutes (service-level fault isolation)
- **API latency**: P99 < 500ms (with caching)
- **Availability**: 99.5% → 99.9% (per-service SLAs)
- **Test coverage**: 70% → 85%

---

## Timeline

| Phase | Duration | Deliverables |
|-------|----------|--------------|
| Phase 1: Extract Services | 12 weeks | 7 independent services, shared Contracts NuGet |
| Phase 2: Add Gateway & Messaging | 4 weeks | API Gateway, RabbitMQ integration, health checks |
| Phase 3: Automate & Observe | 4 weeks | Kubernetes manifests, ELK stack, ArgoCD |
| Phase 4: Advanced Patterns | 8 weeks | Event sourcing, CQRS, Istio mesh |

---

## Document Version
- **Version**: 1.0
- **Last Updated**: 2026-05-12
- **Next Review**: 2026-06-12
