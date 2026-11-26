using System.Text.RegularExpressions;
using HamzetWasl.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.SqlClient;

namespace HamzetWasl.Api.Controllers;

/// <summary>
/// Public landing-site endpoints: launch-list interest, contact messages, newsletter, public counters.
/// Uses its own LANDING_* tables, created on first use, so the authoritative schema script and
/// the EF model are untouched. Reads for admins use the existing "Admin" policy.
/// </summary>
[Route("api/v1/landing")]
public sealed partial class LandingController : ApiControllerBase
{
    private static readonly string[] Roles = ["volunteer", "beneficiary", "association", "company"];
    private static readonly SemaphoreSlim InitLock = new(1, 1);
    private static bool _initialized;

    private readonly string _connectionString;

    public LandingController(IConfiguration configuration) =>
        _connectionString = configuration.GetConnectionString("DefaultConnection")
                            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");

    public sealed record InterestRequest(string? Role, string? Name, string? Email, string? Phone, string? City, string? Note, string? Website);
    public sealed record ContactRequest(string? Name, string? Email, string? Message, string? Website);
    public sealed record SubscribeRequest(string? Email, string? Website);
    public sealed record LandingStats(int Interests, int Subscribers, IReadOnlyDictionary<string, int> ByRole);

    [GeneratedRegex(@"^[^\s@]+@[^\s@]+\.[^\s@]{2,}$")]
    private static partial Regex EmailRegex();

    private static string Clean(string? v, int max)
    {
        var s = (v ?? string.Empty).Trim();
        return s.Length > max ? s[..max] : s;
    }

    private ObjectResult Invalid(string message) =>
        StatusCode(StatusCodes.Status400BadRequest, ApiResponse<object?>.Fail(
            new ApiError(ErrorCodes.ValidationError, message, null), TraceId));

    private async Task<SqlConnection> OpenAsync(CancellationToken ct)
    {
        var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        if (!_initialized)
        {
            await InitLock.WaitAsync(ct);
            try
            {
                if (!_initialized)
                {
                    const string ddl = """
IF OBJECT_ID(N'dbo.LANDING_INTEREST') IS NULL
  CREATE TABLE dbo.LANDING_INTEREST(Id INT IDENTITY PRIMARY KEY, Role NVARCHAR(20) NOT NULL, Name NVARCHAR(100) NOT NULL,
    Email NVARCHAR(150) NULL, Phone NVARCHAR(30) NULL, City NVARCHAR(80) NULL, Note NVARCHAR(1000) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME());
IF OBJECT_ID(N'dbo.LANDING_MESSAGE') IS NULL
  CREATE TABLE dbo.LANDING_MESSAGE(Id INT IDENTITY PRIMARY KEY, Name NVARCHAR(100) NOT NULL, Email NVARCHAR(150) NOT NULL,
    Message NVARCHAR(2000) NOT NULL, CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME());
IF OBJECT_ID(N'dbo.LANDING_SUBSCRIBER') IS NULL
  CREATE TABLE dbo.LANDING_SUBSCRIBER(Id INT IDENTITY PRIMARY KEY, Email NVARCHAR(150) NOT NULL UNIQUE,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME());
""";
                    await using var cmd = new SqlCommand(ddl, conn);
                    await cmd.ExecuteNonQueryAsync(ct);
                    _initialized = true;
                }
            }
            finally { InitLock.Release(); }
        }
        return conn;
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("interest")]
    public async Task<IActionResult> Interest([FromBody] InterestRequest r, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(r.Website)) return StatusCode(201, Envelope(new { ok = true })); // honeypot
        var role = Clean(r.Role, 20).ToLowerInvariant();
        var name = Clean(r.Name, 100); var email = Clean(r.Email, 150); var phone = Clean(r.Phone, 30);
        if (!Roles.Contains(role) || name.Length == 0 || (!EmailRegex().IsMatch(email) && phone.Length == 0))
            return Invalid("بيانات غير صحيحة.");
        await using var conn = await OpenAsync(ct);
        await using var cmd = new SqlCommand(
            "INSERT INTO dbo.LANDING_INTEREST(Role,Name,Email,Phone,City,Note) VALUES(@r,@n,@e,@p,@c,@o)", conn);
        cmd.Parameters.AddWithValue("@r", role); cmd.Parameters.AddWithValue("@n", name);
        cmd.Parameters.AddWithValue("@e", (object?)(email.Length == 0 ? null : email) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@p", (object?)(phone.Length == 0 ? null : phone) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@c", Clean(r.City, 80)); cmd.Parameters.AddWithValue("@o", Clean(r.Note, 1000));
        await cmd.ExecuteNonQueryAsync(ct);
        return CreatedEnvelope(new { ok = true });
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("contact")]
    public async Task<IActionResult> Contact([FromBody] ContactRequest r, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(r.Website)) return StatusCode(201, Envelope(new { ok = true }));
        var name = Clean(r.Name, 100); var email = Clean(r.Email, 150); var msg = Clean(r.Message, 2000);
        if (name.Length == 0 || !EmailRegex().IsMatch(email) || msg.Length < 5) return Invalid("بيانات غير صحيحة.");
        await using var conn = await OpenAsync(ct);
        await using var cmd = new SqlCommand("INSERT INTO dbo.LANDING_MESSAGE(Name,Email,Message) VALUES(@n,@e,@m)", conn);
        cmd.Parameters.AddWithValue("@n", name); cmd.Parameters.AddWithValue("@e", email); cmd.Parameters.AddWithValue("@m", msg);
        await cmd.ExecuteNonQueryAsync(ct);
        return CreatedEnvelope(new { ok = true });
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("subscribe")]
    public async Task<IActionResult> Subscribe([FromBody] SubscribeRequest r, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(r.Website)) return StatusCode(201, Envelope(new { ok = true }));
        var email = Clean(r.Email, 150).ToLowerInvariant();
        if (!EmailRegex().IsMatch(email)) return Invalid("بيانات غير صحيحة.");
        await using var conn = await OpenAsync(ct);
        await using var cmd = new SqlCommand(
            "IF NOT EXISTS(SELECT 1 FROM dbo.LANDING_SUBSCRIBER WHERE Email=@e) INSERT INTO dbo.LANDING_SUBSCRIBER(Email) VALUES(@e)", conn);
        cmd.Parameters.AddWithValue("@e", email);
        await cmd.ExecuteNonQueryAsync(ct);
        return CreatedEnvelope(new { ok = true });
    }

    [AllowAnonymous]
    [HttpGet("stats")]
    public async Task<ActionResult<ApiResponse<LandingStats>>> Stats(CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct);
        var byRole = Roles.ToDictionary(x => x, _ => 0);
        var total = 0;
        await using (var cmd = new SqlCommand("SELECT Role, COUNT(*) FROM dbo.LANDING_INTEREST GROUP BY Role", conn))
        await using (var rd = await cmd.ExecuteReaderAsync(ct))
            while (await rd.ReadAsync(ct)) { var k = rd.GetString(0); var c = rd.GetInt32(1); byRole[k] = c; total += c; }
        int subs;
        await using (var cmd = new SqlCommand("SELECT COUNT(*) FROM dbo.LANDING_SUBSCRIBER", conn))
            subs = (int)(await cmd.ExecuteScalarAsync(ct))!;
        return Ok(Envelope(new LandingStats(total, subs, byRole)));
    }

    [Authorize(Policy = "Admin")]
    [HttpGet("admin/{kind}")]
    public async Task<IActionResult> AdminList(string kind, CancellationToken ct)
    {
        var sql = kind switch
        {
            "interests" => "SELECT TOP 500 Id,Role,Name,Email,Phone,City,Note,CreatedAt FROM dbo.LANDING_INTEREST ORDER BY Id DESC",
            "messages" => "SELECT TOP 500 Id,Name,Email,Message,CreatedAt FROM dbo.LANDING_MESSAGE ORDER BY Id DESC",
            "subscribers" => "SELECT TOP 500 Id,Email,CreatedAt FROM dbo.LANDING_SUBSCRIBER ORDER BY Id DESC",
            _ => null
        };
        if (sql is null) return NotFound();
        await using var conn = await OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        await using var rd = await cmd.ExecuteReaderAsync(ct);
        var rows = new List<Dictionary<string, object?>>();
        while (await rd.ReadAsync(ct))
        {
            var row = new Dictionary<string, object?>();
            for (var i = 0; i < rd.FieldCount; i++) row[char.ToLowerInvariant(rd.GetName(i)[0]) + rd.GetName(i)[1..]] = rd.IsDBNull(i) ? null : rd.GetValue(i);
            rows.Add(row);
        }
        return Ok(Envelope(rows));
    }
}
