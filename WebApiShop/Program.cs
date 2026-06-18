using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using NLog.Web;
using Polly;
using Polly.Extensions.Http;
using Repositories;
using Repository;
using Services;
using StackExchange.Redis;
using System.Text;
using WebApiShop;
using WebApiShop.Middleware;

DotNetEnv.Env.Load();
var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ShopContext>(option => option.UseSqlServer(connectionString));

builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IPasswordService, PasswordService>();
builder.Services.AddScoped<IUsersServices, UsersServices>();
builder.Services.AddScoped<IUsersRepository, UsersRepository>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<ICategoriesServies, CategoriesServies>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IProductImageService, ProductImageService>();
builder.Services.AddScoped<IProductImageRepository, ProductImageRepository>();
builder.Services.AddScoped<IRatingService, RatingService>();
builder.Services.AddScoped<IRatingRepository, RatingRepository>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<IPropertyInquiryService, PropertyInquiryService>();
builder.Services.AddScoped<IPropertyInquiryRepository, PropertyInquiryRepository>();
builder.Services.AddScoped<IAdminInquiryRepository, AdminInquiryRepository>();
builder.Services.AddScoped<IEmailService, EmailService>();

var jwtSettings = builder.Configuration.GetSection("Jwt");
var key = Encoding.UTF8.GetBytes(jwtSettings["Key"]!);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(key)
    };

    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            if (context.Request.Cookies.TryGetValue("jwt", out string? token))
                context.Token = token;
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

var redisConnectionString = builder.Configuration.GetConnectionString("Redis");

var connectionString_ = redisConnectionString ?? "localhost:6379";

var redis = ConnectionMultiplexer.Connect(connectionString_);
builder.Services.AddSingleton<IConnectionMultiplexer>(redis);
builder.Services.AddScoped<ICacheService, CacheService>();


builder.Services.AddSingleton<IKafkaProducerService, KafkaProducerService>();

// ── HTTP Client with Polly Retry Policy ───────────────────────────────────────
// Retries on transient errors (5xx, network failures) and 429 Too Many Requests
// Exponential backoff: 500ms, 1000ms, 2000ms
builder.Services.AddHttpClient("RealEstateClient")
    .AddPolicyHandler(HttpPolicyExtensions
        .HandleTransientHttpError()                          
        .OrResult(msg => msg.StatusCode ==
            System.Net.HttpStatusCode.TooManyRequests)       
        .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: (retryAttempt, response, _) =>
            {
                // If server sends Retry-After header, respect it
                var retryAfter = response?.Result?.Headers?.RetryAfter?.Delta;
                return retryAfter ?? TimeSpan.FromMilliseconds(500 * Math.Pow(2, retryAttempt - 1));
            },
            onRetryAsync: (outcome, timespan, retryAttempt, _) =>
            {
                Console.WriteLine($"[Polly] Retry {retryAttempt}/3 after {timespan.TotalMilliseconds}ms — Reason: {outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString()}");
                return Task.CompletedTask;
            })
    );

builder.Host.UseNLog();

builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Real Estate API", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste your JWT token here (without 'Bearer ' prefix). The token is also read automatically from the 'jwt' cookie."
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()
              .WithExposedHeaders("IsAdmin");
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseCors();

app.UseErrorHandling();
app.UseFixedWindowRateLimiter();
app.UseMiddleware<AdminAuthorizationMiddleware>();
app.UseRating();

app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();  
app.UseAuthorization();

app.MapControllers();

app.Run();
