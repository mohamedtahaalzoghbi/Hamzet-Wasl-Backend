using FluentAssertions;
using HamzetWasl.Application.Common;
using HamzetWasl.Application.Contracts.Auth;
using HamzetWasl.Application.Interfaces;
using HamzetWasl.Domain.Constants;
using HamzetWasl.Domain.Entities;
using HamzetWasl.Infrastructure.Configuration;
using HamzetWasl.Infrastructure.Identity;
using HamzetWasl.Infrastructure.Persistence;
using HamzetWasl.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HamzetWasl.UnitTests;

/// <summary>
/// Coverage for the staged registration flow: no account, phone or email is persisted
/// before the phone OTP succeeds (the behavior the Flutter client reported and the
/// product owner confirmed). The pending state lives in the distributed cache; these
/// tests use a real MemoryDistributedCache and the real OtpService.
/// </summary>
public sealed class RegistrationFlowTests : SqliteServiceTestBase
{
    private static readonly IOptions<OtpOptions> OtpOpts = Options.Create(new OtpOptions
    {
        HashKey = "unit-test-otp-hash-key-0123456789",
        ExpiryMinutes = 10,
        MaxAttempts = 3,
        ResendCooldownSeconds = 60
    });

    private sealed class FakeSms : ISmsSender
    {
        public List<(string Phone, string Message)> Sent { get; } = [];

        public string? LastCode => Sent.Count == 0
            ? null
            : System.Text.RegularExpressions.Regex.Match(Sent[^1].Message, @"(\d{6})").Groups[1].Value;

        public Task SendAsync(string phone, string message, CancellationToken ct = default)
        {
            Sent.Add((phone, message));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeEmail : IEmailSender
    {
        public Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeTokens : ITokenService
    {
        public AccessTokenResult CreateAccessToken(UserSessionDto session) =>
            new("access-token", DateTime.UtcNow.AddHours(1));

        public RefreshTokenResult CreateRefreshToken(Guid userId) =>
            new("refresh-token", DateTime.UtcNow.AddDays(7), Guid.NewGuid().ToString("N"));

        public RefreshTokenPayload? ValidateRefreshToken(string token) => null;
        public string HashRefreshToken(string token) => token;
        public string CreateGoogleRegistrationTicket(string email, string? name, string googleSubject) => "ticket";
        public GoogleRegistrationTicketPayload? ValidateGoogleRegistrationTicket(string ticket) => null;
    }

    private sealed class FakeGoogle : IGoogleTokenValidator
    {
        public GoogleUserInfo? Result { get; set; }

        public Task<GoogleUserInfo?> ValidateAsync(string idToken, CancellationToken ct = default) =>
            Task.FromResult(Result);
    }

    private (AuthService Auth, UserManager<AppUser> Users, FakeSms Sms) NewAuth(
        IDistributedCache? cache = null, IGoogleTokenValidator? google = null)
    {
        cache ??= new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var sms = new FakeSms();

        var services = new ServiceCollection();
        services.AddSingleton<HamzetWaslDbContext>(Db);
        services.AddLogging();
        services.AddDataProtection();
        services.AddIdentityCore<AppUser>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<HamzetWaslDbContext>()
            .AddDefaultTokenProviders();
        var provider = services.BuildServiceProvider();
        var users = provider.GetRequiredService<UserManager<AppUser>>();

        var otp = new OtpService(OtpOpts, cache, Clock);
        var auth = new AuthService(Db, users, new FakeTokens(), otp, new FakeEmail(), sms,
            google ?? new FakeGoogle(), cache, Clock, NullLogger<AuthService>.Instance);
        return (auth, users, sms);
    }

    private void SeedRole(string roleName)
    {
        Db.AccountRoles.Add(new AccountRole { RoleId = 1, RoleName = roleName });
        Db.SaveChanges();
    }

    private static RegisterRequest VolunteerRequest(string email = "new@example.com", string phone = "01012345678") =>
        new()
        {
            Role = RoleNames.Volunteer,
            Name = "متطوع جديد",
            Email = email,
            Phone = phone,
            Password = "password123",
            ConfirmPassword = "password123",
            TermsAccepted = true
        };

    [Fact]
    public async Task Register_keeps_the_database_empty_until_the_phone_is_verified()
    {
        SeedRole(RoleNames.Volunteer);
        var (auth, _, sms) = NewAuth();

        var pending = await auth.RegisterAsync(VolunteerRequest());

        pending.RequiresPhoneVerification.Should().BeTrue();
        pending.RegistrationTicket.Should().NotBeNullOrWhiteSpace();
        sms.Sent.Should().ContainSingle(s => s.Phone == pending.Phone);

        (await Db.UserAccounts.AnyAsync()).Should().BeFalse("no account row exists before OTP");
        (await Db.UserProfiles.AnyAsync()).Should().BeFalse();
        (await Db.ConsentRecords.AnyAsync()).Should().BeFalse();
        (await Db.Users.AnyAsync()).Should().BeFalse("no Identity user exists before OTP");
    }

    [Fact]
    public async Task Complete_creates_a_phone_verified_account_and_a_working_password()
    {
        SeedRole(RoleNames.Volunteer);
        var (auth, users, sms) = NewAuth();
        var pending = await auth.RegisterAsync(VolunteerRequest());

        var response = await auth.CompleteRegistrationAsync(new CompleteRegistrationRequest
        {
            RegistrationTicket = pending.RegistrationTicket,
            Code = sms.LastCode!
        });

        response.AccessToken.Should().Be("access-token");
        var account = await Db.UserAccounts.SingleAsync();
        account.PhoneVerified.Should().BeTrue("the phone was proven by OTP");
        account.EmailVerified.Should().BeFalse("email verification stays a separate step");
        account.Phone.Should().Be(pending.Phone);
        (await Db.VolunteerProfiles.AnyAsync(v => v.UserId == account.UserId)).Should().BeTrue();
        (await Db.ConsentRecords.AnyAsync(c => c.UserId == account.UserId)).Should().BeTrue();

        var appUser = await users.FindByIdAsync(account.UserId.ToString());
        appUser.Should().NotBeNull();
        appUser!.PhoneNumberConfirmed.Should().BeTrue();
        (await users.CheckPasswordAsync(appUser, "password123")).Should().BeTrue(
            "the hash produced at step 1 must still log the user in");
    }

    [Fact]
    public async Task Complete_rejects_a_second_use_of_the_same_ticket()
    {
        SeedRole(RoleNames.Volunteer);
        var (auth, _, sms) = NewAuth();
        var pending = await auth.RegisterAsync(VolunteerRequest());
        await auth.CompleteRegistrationAsync(new CompleteRegistrationRequest
        {
            RegistrationTicket = pending.RegistrationTicket,
            Code = sms.LastCode!
        });

        var again = async () => await auth.CompleteRegistrationAsync(new CompleteRegistrationRequest
        {
            RegistrationTicket = pending.RegistrationTicket,
            Code = sms.LastCode!
        });

        var ex = await again.Should().ThrowAsync<AppException>();
        ex.Which.Code.Should().Be(ErrorCodes.RegistrationExpired);
        (await Db.UserAccounts.CountAsync()).Should().Be(1, "one ticket creates exactly one account");
    }

    [Fact]
    public async Task An_abandoned_registration_does_not_block_registering_again()
    {
        SeedRole(RoleNames.Volunteer);

        // First attempt is abandoned before the code arrives; the second attempt happens
        // after an API restart (fresh cache), which is when the old flow returned 409.
        var (firstAuth, _, _) = NewAuth();
        await firstAuth.RegisterAsync(VolunteerRequest());

        var (retryAuth, _, retrySms) = NewAuth();
        var retried = await retryAuth.RegisterAsync(VolunteerRequest());

        retried.RegistrationTicket.Should().NotBeNullOrWhiteSpace(
            "nothing was persisted by the abandoned attempt, so there is nothing to conflict with");
        var response = await retryAuth.CompleteRegistrationAsync(new CompleteRegistrationRequest
        {
            RegistrationTicket = retried.RegistrationTicket,
            Code = retrySms.LastCode!
        });
        response.Phone.Should().Be(retried.Phone);
    }

    [Fact]
    public async Task Wrong_codes_are_limited_and_then_even_the_right_code_is_rejected()
    {
        SeedRole(RoleNames.Volunteer);
        var (auth, _, sms) = NewAuth();
        var pending = await auth.RegisterAsync(VolunteerRequest());
        var correct = sms.LastCode!;
        var wrong = correct == "000000" ? "000001" : "000000";

        for (var i = 0; i < 3; i++)
        {
            var attempt = async () => await auth.CompleteRegistrationAsync(new CompleteRegistrationRequest
            {
                RegistrationTicket = pending.RegistrationTicket,
                Code = wrong
            });
            (await attempt.Should().ThrowAsync<AppException>()).Which.Code.Should().Be(ErrorCodes.OtpInvalid);
        }

        var locked = async () => await auth.CompleteRegistrationAsync(new CompleteRegistrationRequest
        {
            RegistrationTicket = pending.RegistrationTicket,
            Code = correct
        });
        (await locked.Should().ThrowAsync<AppException>()).Which.Code.Should().Be(ErrorCodes.OtpTooManyAttempts);
        (await Db.UserAccounts.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task An_expired_code_is_rejected()
    {
        SeedRole(RoleNames.Volunteer);
        var (auth, _, sms) = NewAuth();
        var pending = await auth.RegisterAsync(VolunteerRequest());

        Clock.UtcNow = Clock.UtcNow.AddMinutes(11);

        var complete = async () => await auth.CompleteRegistrationAsync(new CompleteRegistrationRequest
        {
            RegistrationTicket = pending.RegistrationTicket,
            Code = sms.LastCode!
        });
        (await complete.Should().ThrowAsync<AppException>()).Which.Code.Should().Be(ErrorCodes.OtpExpired);
        (await Db.UserAccounts.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Complete_conflicts_cleanly_when_the_email_was_taken_after_the_start()
    {
        SeedRole(RoleNames.Volunteer);
        var (auth, _, sms) = NewAuth();
        var pending = await auth.RegisterAsync(VolunteerRequest());

        // Another registration completed for the same email while the code was on its way
        // (a second context stands in for the other request).
        await using var otherDb = NewSecondContext();
        otherDb.UserAccounts.Add(new UserAccount
        {
            UserId = Guid.NewGuid(),
            RoleId = 1,
            Email = "new@example.com",
            EmailVerified = true,
            PhoneVerified = true,
            AccountStatus = AccountStatuses.Active,
            CreatedAt = Clock.UtcNow
        });
        await otherDb.SaveChangesAsync();

        var complete = async () => await auth.CompleteRegistrationAsync(new CompleteRegistrationRequest
        {
            RegistrationTicket = pending.RegistrationTicket,
            Code = sms.LastCode!
        });
        (await complete.Should().ThrowAsync<AppException>()).Which.Code.Should().Be(ErrorCodes.EmailAlreadyExists);
        (await Db.UserAccounts.CountAsync()).Should().Be(1, "only the squatter's account exists");
    }

    [Fact]
    public async Task Resend_cannot_keep_a_pending_registration_alive_forever()
    {
        SeedRole(RoleNames.Volunteer);
        var (auth, _, _) = NewAuth();
        var pending = await auth.RegisterAsync(VolunteerRequest());

        // The flow is bounded to three code lifetimes (30 minutes) from the first issue.
        Clock.UtcNow = Clock.UtcNow.AddMinutes(31);

        var resend = async () => await auth.ResendRegistrationCodeAsync(new ResendRegistrationCodeRequest
        {
            RegistrationTicket = pending.RegistrationTicket
        });
        (await resend.Should().ThrowAsync<AppException>()).Which.Code.Should().Be(ErrorCodes.RegistrationExpired);
    }

    [Fact]
    public async Task Google_signup_also_creates_nothing_until_the_phone_is_verified()
    {
        SeedRole(RoleNames.Volunteer);
        var google = new FakeGoogle
        {
            Result = new GoogleUserInfo("google-subject-1", "google@example.com", "مستخدم جوجل", true)
        };
        var (auth, _, sms) = NewAuth(google: google);

        var response = await auth.GoogleAsync(new GoogleAuthRequest
        {
            IdToken = "token",
            Role = RoleNames.Volunteer,
            Phone = "01012345678",
            TermsAccepted = true
        });

        response.Auth.Should().BeNull("no session before the phone OTP succeeds");
        response.PhoneVerificationRequired.Should().NotBeNull();
        (await Db.UserAccounts.AnyAsync()).Should().BeFalse("Google registration must not bypass phone verification");

        var completed = await auth.CompleteRegistrationAsync(new CompleteRegistrationRequest
        {
            RegistrationTicket = response.PhoneVerificationRequired!.RegistrationTicket,
            Code = sms.LastCode!
        });

        completed.EmailVerified.Should().BeTrue("Google already proved the email");
        completed.PhoneVerified.Should().BeTrue();
        var account = await Db.UserAccounts.SingleAsync();
        account.Email.Should().Be("google@example.com");
    }
}
