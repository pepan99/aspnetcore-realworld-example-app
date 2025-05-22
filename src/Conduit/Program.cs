using System;
using System.Collections.Generic;
using Conduit;
using Conduit.Infrastructure;
using Conduit.Infrastructure.Errors;
using Conduit.Infrastructure.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;



var builder = WebApplication.CreateBuilder(args);

var connectionString = String.Empty;


if (builder.Environment.IsDevelopment())
{
    connectionString = builder.Configuration.GetConnectionString("DB_CONNECTION_STRING");
    if (string.IsNullOrEmpty(connectionString))
    {
        Console.WriteLine("[WARNING] Development: DB_CONNECTION_STRING not found. Check appsettings.Development.json or environment variables.");
    }
}
else
{
    connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");
    if (string.IsNullOrEmpty(connectionString))
    {
        Console.WriteLine("[CRITICAL ERROR] Production/Staging: DB_CONNECTION_STRING environment variable is not set.");
    }
}

if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("Database connection string ('DB_CONNECTION_STRING') is not configured. Application cannot start.");
}

builder.Services.AddDbContext<ConduitContext>(options =>
{
    options.UseSqlServer(connectionString, sqlServerOptionsAction: optBuilder =>
    {
        optBuilder.EnableRetryOnFailure(
            maxRetryCount: 15,
            maxRetryDelay: TimeSpan.FromSeconds(500),
            errorNumbersToAdd: null);
    });
});

builder.Services.AddLocalization(x => x.ResourcesPath = "Resources");

builder.Services.AddSwaggerGen(x =>
{
    x.AddSecurityDefinition(
        "Bearer",
        new OpenApiSecurityScheme
        {
            In = ParameterLocation.Header,
            Description = "Please insert JWT with Bearer into field",
            Name = "Authorization",
            Type = SecuritySchemeType.ApiKey,
            BearerFormat = "JWT"
        }
    );

    x.SupportNonNullableReferenceTypes();

    x.AddSecurityRequirement(
        new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        }
    );
    x.SwaggerDoc("v1", new OpenApiInfo { Title = "RealWorld API", Version = "v1" });
    x.CustomSchemaIds(y => y.FullName);
    x.DocInclusionPredicate((docName, apiDesc) => !string.IsNullOrWhiteSpace(apiDesc.GroupName));
    x.TagActionsBy(y => new List<string> { y.GroupName ?? throw new InvalidOperationException("Action must have a group name for Swagger.") });
    x.CustomSchemaIds(s => s.FullName?.Replace("+", "."));
});

builder.Services.AddCors();
builder
    .Services.AddMvc(opt =>
    {
        opt.Conventions.Add(new GroupByApiRootConvention());
        opt.Filters.Add(typeof(ValidatorActionFilter));
        opt.EnableEndpointRouting = false;
    })
    .AddJsonOptions(opt =>
        opt.JsonSerializerOptions.DefaultIgnoreCondition = System
            .Text
            .Json
            .Serialization
            .JsonIgnoreCondition
            .WhenWritingNull
    );

builder.Services.AddConduit();
builder.Services.AddJwt();

var app = builder.Build();


app.UseMiddleware<ErrorHandlingMiddleware>();

app.UseCors(x => x.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());

app.UseAuthentication();

app.UseMvc();

app.UseSwagger(c => c.RouteTemplate = "swagger/{documentName}/swagger.json");
app.UseSwaggerUI(x => x.SwaggerEndpoint("/swagger/v1/swagger.json", "RealWorld API V1"));


using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    var env = services.GetRequiredService<IHostEnvironment>();
    var context = services.GetRequiredService<ConduitContext>();
    try
    {
        logger.LogInformation("Attempting database initialization...");

        if (env.IsDevelopment())
        {
            logger.LogInformation("Development environment: Calling Database.EnsureCreated().");
            var created = context.Database.EnsureCreated();
            if (created)
            {
                logger.LogInformation("Database was created by EnsureCreated(). You might want to seed initial data here.");

            }
            else
            {
                logger.LogInformation("Database already existed or was not created by EnsureCreated().");
            }
        }
        else
        {

            logger.LogInformation("Non-Development environment: Applying database migrations.");
            context.Database.Migrate();
            logger.LogInformation("Database migrations applied successfully (or no pending migrations).");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred during database initialization (EnsureCreated or Migrate). " +
                            "The application will start, but database-dependent features might be unavailable " +
                            "until the database is accessible and correctly initialized.");

    }
}
app.Run();
