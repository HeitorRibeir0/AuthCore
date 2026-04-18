using System.Net;
using System.Net.Http.Json;
using AuthCore.API.DTOs;
using AuthCore.API.Infrastructure.Data;
using DotNet.Testcontainers.Builders;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace AuthCore.Tests.Integration;

public class AuthIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("authcore_test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                if (descriptor is not null)
                    services.Remove(descriptor);

                services.AddDbContext<AppDbContext>(options =>
                    options.UseNpgsql(_postgres.GetConnectionString()));
            });
        });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task FullLoginFlow_RegisterThenLogin_ReturnsAccessToken()
    {
        var register = await _client.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest("Alice", "alice@flow.com", "Password1"));
        register.StatusCode.Should().Be(HttpStatusCode.Created);

        var login = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest("alice@flow.com", "Password1"));
        login.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await login.Content.ReadFromJsonAsync<AuthResponse>();
        body!.AccessToken.Should().NotBeNullOrEmpty();
        login.Headers.Should().ContainKey("Set-Cookie");
    }

    [Fact]
    public async Task FullRefreshFlow_LoginThenRefresh_ReturnsNewAccessToken()
    {
        await _client.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest("Bob", "bob@flow.com", "Password1"));

        var login = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest("bob@flow.com", "Password1"));
        login.StatusCode.Should().Be(HttpStatusCode.OK);

        // cookie é enviado automaticamente pelo HttpClient do WebApplicationFactory
        var refresh = await _client.PostAsync("/api/v1/auth/refresh", null);
        refresh.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await refresh.Content.ReadFromJsonAsync<AuthResponse>();
        body!.AccessToken.Should().NotBeNullOrEmpty();
    }
}
