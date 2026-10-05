using System;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Autofac;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;
using Volo.Abp.Swashbuckle;
using Volo.Abp.Threading;
using WashGo.Application;
using WashGo.Core.Endpoint;
using WashGo.EntityFrameworkCore;
using WashGo.EntityFrameworkCore.EntityFrameworkCore;
using WashGo.HttpApi;

namespace WashGo.HttpApi.Host
{
    [DependsOn(
        typeof(WashGoHttpApiModule),
        typeof(WashGoApplicationModule),
        typeof(WashGoEntityFrameworkCoreModule),
        typeof(AbpAutofacModule),
        typeof(AbpSwashbuckleModule)
    )]
    public class WashGoHttpApiHostModule : AbpModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            var configuration = context.Services.GetConfiguration();

            Configure<AbpDbContextOptions>(options =>
            {
                options.UseNpgsql();
            });

            // JWT Authentication Configuration
            var secretKey = configuration["Jwt:SecretKey"] ?? "WashGo_Super_Secret_Key_For_Jwt_Token_Generation_2026!#@$";
            var issuer = configuration["Jwt:Issuer"] ?? "WashGoBackend";
            var audience = configuration["Jwt:Audience"] ?? "WashGoClients";

            context.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = false;
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };
            });

            context.Services.AddAuthorization(options =>
            {
                options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
                options.AddPolicy("MerchantOrAdmin", policy => policy.RequireRole("Admin", "Merchant"));
                options.AddPolicy("ShipperOrAdmin", policy => policy.RequireRole("Admin", "Shipper"));
            });

            context.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(builder =>
                {
                    builder
                        .WithOrigins(
                            configuration["App:CorsOrigins"]?
                                .Split(",", StringSplitOptions.RemoveEmptyEntries)
                                .Select(o => o.Trim())
                                .ToArray() ?? ["http://localhost:5173", "http://localhost:3000"]
                        )
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                });
            });

            context.Services.AddEndpointsApiExplorer();
            context.Services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "WashGo API - Hệ thống Giặt sấy & Tủ đồ thông minh",
                    Version = "v1",
                    Description = "RESTful & Minimal APIs cho hệ thống WashGo (DDD + Clean Architecture + ABP Framework)"
                });
                options.CustomSchemaIds(type => type.FullName);

                // Add JWT Security Definition for Swagger UI
                var securityScheme = new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Description = "Nhập JWT Bearer token theo định dạng: Bearer {token}",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Reference = new OpenApiReference
                    {
                        Id = JwtBearerDefaults.AuthenticationScheme,
                        Type = ReferenceType.SecurityScheme
                    }
                };

                options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, securityScheme);
                options.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    { securityScheme, Array.Empty<string>() }
                });
            });
        }

        public override void OnApplicationInitialization(ApplicationInitializationContext context)
        {
            var app = context.GetApplicationBuilder();
            var env = context.GetEnvironment();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseStaticFiles();
            app.UseRouting();
            app.UseCors();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "WashGo API v1");
                options.RoutePrefix = "swagger";
            });

            // Seed data on startup
            using (var scope = context.ServiceProvider.CreateScope())
            {
                try
                {
                    var seeder = scope.ServiceProvider.GetRequiredService<IDataSeeder>();
                    AsyncHelper.RunSync(() => seeder.SeedAsync());
                }
                catch (Exception ex)
                {
                    // Log or handle seed error gracefully if DB not yet migrated or during EF migrations
                    Console.WriteLine($"[DataSeeder] Seed warning: {ex.Message}");
                }
            }

            // Map all Minimal API endpoints implementing IEndpointBase
            app.UseEndpoints(endpoints =>
            {
                var handlers = context.ServiceProvider.GetServices<IEndpointBase>();
                foreach (var handler in handlers)
                {
                    handler.MapEndpoint(endpoints);
                }
            });
        }
    }
}
