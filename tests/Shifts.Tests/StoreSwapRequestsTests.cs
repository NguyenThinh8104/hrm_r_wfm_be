using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Domain.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Modules.Shifts.Controllers;
using Modules.Shifts.DTOs;
using Modules.Shifts.Interfaces;
using Modules.Shifts.Services;
using Shared.Common;
using Shared.Data;
using Xunit;

namespace Shifts.Tests;

public class StoreSwapRequestsTests
{
    [Fact]
    public async Task Production_mysql_provider_translates_expiry_statistics_search_and_paging()
    {
        var commands = new MySqlTranslationProbe();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseMySql("Server=127.0.0.1;Database=unused;User Id=unused;Password=unused;",
                new MySqlServerVersion(new Version(8, 0, 36)), mysql => mysql.UseNetTopologySuite())
            .AddInterceptors(new NoDatabaseConnection(), commands).Options;
        await using var db = new AppDbContext(options);
        var service = new ShiftSwapService(db, null!, Microsoft.Extensions.Logging.Abstractions.NullLogger<ShiftSwapService>.Instance);
        var response = await service.GetSwapRequestsByStoreAsync(3,
            new StoreSwapRequestsQueryDto { Status = "PENDING", Search = "test", Page = 2, PageSize = 5 });
        Assert.True(response.Success);
        Assert.Contains(commands.Sql, sql => sql.StartsWith("UPDATE"));
        Assert.Contains(commands.Sql, sql => sql.Contains("GROUP BY") && sql.Contains("COUNT("));
        Assert.Contains(commands.Sql, sql => sql.Contains("LIMIT") && sql.Contains("ORDER BY") && sql.Contains("WHERE"));
    }

    [Fact]
    public async Task Api_filters_pages_and_counts_in_sql_with_branch_wide_statistics()
    {
        await using var api = await ReviewApi.Start();
        var page = await api.Get("?status=PENDING&page=2&pageSize=5");
        Assert.Equal(12, page.TotalItems);
        Assert.Equal(2, page.Page);
        Assert.Equal(5, page.PageSize);
        Assert.Equal(new[] { 7, 6, 5, 4, 3 }, page.Items.Select(x => x.SwapRequestId));
        Assert.All(page.Items, x => Assert.Equal("PENDING", x.Status));
        Assert.Equal(18, page.Statistics.TotalCount);
        Assert.Equal(12, page.Statistics.PendingCount);
        Assert.Equal(1, page.Statistics.ApprovedCount);
        Assert.Equal(1, page.Statistics.RejectedCount);
        Assert.Equal(2, page.Statistics.ExpiredCount);
        Assert.Contains(api.Sql.Commands, sql => sql.Contains("LIMIT") && sql.Contains("OFFSET"));
        Assert.Contains(api.Sql.Commands, sql => sql.Contains("COUNT(*)") && sql.Contains("WHERE"));
    }

    [Theory]
    [InlineData("  SPECIAL  ", 1)]
    [InlineData("legacy role", 18)]
    public async Task Search_is_trimmed_case_insensitive_and_supports_legacy_assignment_fields(string search, int id)
    {
        await using var api = await ReviewApi.Start();
        var page = await api.Get("?status=PENDING&search=" + Uri.EscapeDataString(search));
        Assert.Equal(id, Assert.Single(page.Items).SwapRequestId);
        Assert.Equal(1, page.TotalItems);
        Assert.Equal(18, page.Statistics.TotalCount);
        Assert.Contains(api.Sql.Commands, sql => sql.Contains("instr(") && sql.Contains("lower("));
    }

    [Fact]
    public async Task Expiry_precedes_counts_and_filtering_and_does_not_expire_another_branch()
    {
        await using var api = await ReviewApi.Start();
        var page = await api.Get("?status=EXPIRED");
        Assert.Equal(new[] { 16, 14 }, page.Items.Select(x => x.SwapRequestId));
        Assert.All(page.Items, x => Assert.Equal("EXPIRED", x.Status));
        using var scope = api.App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal("EXPIRED", (await db.ShiftSwapRequests.FindAsync(14UL))!.Status);
        Assert.Equal("PENDING", (await db.ShiftSwapRequests.FindAsync(101UL))!.Status);
    }

    [Fact]
    public async Task Server_clamps_pages_after_the_last_pending_request_is_reviewed()
    {
        await using var api = await ReviewApi.Start();
        Assert.Equal(2, (await api.Get("?status=PENDING&page=2&pageSize=10")).Page);
        using (var scope = api.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.ShiftSwapRequests.Where(x => x.Id == 1 || x.Id == 2)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "APPROVED"));
        }
        var page = await api.Get("?status=PENDING&page=2&pageSize=10");
        Assert.Equal(1, page.Page);
        Assert.Equal(10, page.TotalItems);
        Assert.Equal(10, page.Items.Count);
        var empty = await api.Get("?search=does-not-exist&page=999");
        Assert.Empty(empty.Items);
        Assert.Equal(0, empty.TotalItems);
        Assert.Equal(1, empty.Page);
        Assert.Equal(18, empty.Statistics.TotalCount);
    }

    [Theory]
    [InlineData("?page=0")]
    [InlineData("?page=-1")]
    [InlineData("?pageSize=0")]
    [InlineData("?pageSize=101")]
    [InlineData("?status=INVALID")]
    [InlineData("?page=not-a-number")]
    public async Task Api_rejects_invalid_query_parameters(string query)
    {
        await using var api = await ReviewApi.Start();
        var response = await api.Client.GetAsync("/api/shifts/swap-requests/3" + query);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(api.Sql.Commands);
    }

    [Fact]
    public async Task Manager_is_bound_to_claim_branch_and_alias_returns_same_contract()
    {
        await using var api = await ReviewApi.Start();
        var requestedOtherBranch = await api.Get("", branch: 7);
        Assert.Equal(18, requestedOtherBranch.Statistics.TotalCount);
        Assert.DoesNotContain(requestedOtherBranch.Items, x => x.SwapRequestId == 101);
        var alias = await api.Client.GetFromJsonAsync<ApiResponse<StoreSwapRequestsPageDto>>(
            "/api/shift-swaps/swap-requests/3?status=APPROVED");
        Assert.Equal(12, Assert.Single(alias!.Data!.Items).SwapRequestId);
        api.Client.DefaultRequestHeaders.Remove("X-Test-Branch");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await api.Client.GetAsync("/api/shifts/swap-requests/7")).StatusCode);
        api.Client.DefaultRequestHeaders.Remove("X-Test-Role");
        api.Client.DefaultRequestHeaders.Add("X-Test-Role", "CASHIER");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await api.Client.GetAsync("/api/shifts/swap-requests/3")).StatusCode);
    }

    [Fact]
    public async Task Global_manager_can_select_branch_and_empty_branch_has_zero_statistics()
    {
        await using var api = await ReviewApi.Start();
        api.Client.DefaultRequestHeaders.Remove("X-Test-Role");
        api.Client.DefaultRequestHeaders.Add("X-Test-Role", "OPERATIONS_ADMIN");
        var otherBranch = await api.Get("", branch: 7);
        Assert.Equal(101, Assert.Single(otherBranch.Items).SwapRequestId);
        Assert.Equal(1, otherBranch.Statistics.TotalCount);
        var emptyBranch = await api.Get("", branch: 999);
        Assert.Empty(emptyBranch.Items);
        Assert.Equal(0, emptyBranch.Statistics.TotalCount);
        Assert.Equal(0, emptyBranch.TotalItems);
        Assert.Equal(1, emptyBranch.Page);
    }
}

// Real HTTP controller + real relational queries, isolated from the developer's database.
internal sealed class ReviewApi(WebApplication app, SqliteConnection connection, HttpClient client, SqlCapture sql) : IAsyncDisposable
{
    public WebApplication App => app;
    public HttpClient Client => client;
    public SqlCapture Sql => sql;

    public static async Task<ReviewApi> Start()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var sql = new SqlCapture();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection)
            .AddInterceptors(sql).Options;
        await using (var db = new ReviewTestDb(options))
        {
            await db.Database.EnsureCreatedAsync();
            await Seed(db);
        }
        sql.Commands.Clear();
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddScoped<AppDbContext>(_ => new ReviewTestDb(options));
        builder.Services.AddScoped<IShiftSwapService>(sp => new ShiftSwapService(
            sp.GetRequiredService<AppDbContext>(), null!, sp.GetRequiredService<ILogger<ShiftSwapService>>()));
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, TestAuthentication>("Test", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddControllers().AddApplicationPart(typeof(ShiftSwapsController).Assembly);
        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        var client = new HttpClient { BaseAddress = new Uri(address) };
        client.DefaultRequestHeaders.Add("X-Test-Role", "STORE_MANAGER");
        client.DefaultRequestHeaders.Add("X-Test-Branch", "3");
        return new ReviewApi(app, connection, client, sql);
    }

    public async Task<StoreSwapRequestsPageDto> Get(string query, int branch = 3)
    {
        using var response = await client.GetAsync($"/api/shifts/swap-requests/{branch}{query}");
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<StoreSwapRequestsPageDto>>();
        Assert.True(envelope!.Success);
        return envelope.Data!;
    }

    public async ValueTask DisposeAsync()
    {
        client.Dispose();
        await app.DisposeAsync();
        await connection.DisposeAsync();
    }

    private static async Task Seed(AppDbContext db)
    {
        db.Roles.AddRange(new Role { Id = 1, RoleCode = "CASHIER", RoleName = "Cashier" },
            new Role { Id = 2, RoleCode = "SALES_STAFF", RoleName = "Legacy role" });
        db.Branches.AddRange(new Branch { Id = 3, BranchCode = "TEST3", Name = "Test 3" },
            new Branch { Id = 7, BranchCode = "TEST7", Name = "Test 7" });
        db.Users.AddRange(new User { Id = 1, EmployeeCode = "TEST1", FullName = "Lan", Email = "lan@example.test", Phone = "0900000001", RoleId = 1 },
            new User { Id = 2, EmployeeCode = "TEST2", FullName = "Mai", Email = "mai@example.test", Phone = "0900000002", RoleId = 1 });
        db.ShiftTemplates.Add(new ShiftTemplate { Id = 1, TemplateCode = "TEST", Name = "Morning", StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(12, 0) });
        var today = DateOnly.FromDateTime(DateTime.Today);
        db.WorkSchedules.AddRange(
            new WorkSchedule { Id = 1, BranchId = 3, ShiftTemplateId = 1, CreatedBy = 1, WorkDate = today.AddDays(2) },
            new WorkSchedule { Id = 2, BranchId = 3, ShiftTemplateId = 1, CreatedBy = 1, WorkDate = today },
            new WorkSchedule { Id = 3, BranchId = 7, ShiftTemplateId = 1, CreatedBy = 1, WorkDate = today.AddDays(-1) });
        db.ShiftAssignments.Add(new ShiftAssignment { Id = 1, ScheduleId = 1, UserId = 1, AssignedRoleId = 2 });
        var statuses = Enumerable.Repeat("PENDING", 11)
            .Concat(new[] { "APPROVED", "REJECTED", "PENDING_PEER", "CANCELLED", "EXPRIED", "PENDING_PEER" }).ToArray();
        for (var i = 0; i < statuses.Length; i++)
            db.ShiftSwapRequests.Add(new ShiftSwapRequest
            {
                Id = (ulong)i + 1, ScheduleId = i == 13 ? 2UL : 1UL, RequesterUserId = 1,
                TargetUserId = 2, RequestType = i % 2 == 0 ? "LEAVE" : "SWAP", Status = statuses[i],
                Reason = i == 0 ? "special reason" : null, CreatedAt = new DateTime(2026, 1, 1)
            });
        db.ShiftSwapRequests.AddRange(
            new ShiftSwapRequest { Id = 18, RequestingAssignmentId = 1, Status = "PENDING", CreatedAt = new DateTime(2026, 1, 1) },
            new ShiftSwapRequest { Id = 101, ScheduleId = 3, RequesterUserId = 2, Status = "PENDING" });
        await db.SaveChangesAsync();
    }
}

internal sealed class ReviewTestDb(DbContextOptions<AppDbContext> options) : AppDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // SQLite has no spatial/unsigned column types. Keep production relationships/projections.
        modelBuilder.Entity<Branch>().Ignore(x => x.Location);
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
            foreach (var property in entity.GetProperties())
            {
                if (property.ClrType == typeof(ulong) || property.ClrType == typeof(ulong?))
                    property.SetValueConverter(new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<ulong, long>(value => (long)value, value => (ulong)value));
            }
    }
}

internal sealed class SqlCapture : DbCommandInterceptor
{
    public List<string> Commands { get; } = [];
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        Commands.Add(command.CommandText);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }
}

internal sealed class TestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "1"), new("EmployeeId", "1"),
            new(ClaimTypes.Role, Request.Headers["X-Test-Role"].ToString()) };
        if (Request.Headers.TryGetValue("X-Test-Branch", out var branch)) claims.Add(new Claim("StoreId", branch.ToString()));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
    }
}

// Exercise Pomelo's production SQL translator without opening a database connection.
internal sealed class NoDatabaseConnection : DbConnectionInterceptor
{
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection,
        ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(InterceptionResult.Suppress());
}

internal sealed class MySqlTranslationProbe : DbCommandInterceptor
{
    public List<string> Sql { get; } = [];
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Sql.Add(command.CommandText);
        return ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(0));
    }
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        Sql.Add(command.CommandText);
        var table = new System.Data.DataTable();
        if (command.CommandText.StartsWith("SELECT COUNT(*)") && !command.CommandText.Contains("GROUP BY"))
        {
            table.Columns.Add("count", typeof(int));
            table.Rows.Add(0);
        }
        return ValueTask.FromResult(InterceptionResult<DbDataReader>.SuppressWithResult(table.CreateDataReader()));
    }
}
