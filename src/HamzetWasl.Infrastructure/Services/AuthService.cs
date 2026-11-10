using System.Security.Claims;
using HamzetWasl.Application.Common;
using HamzetWasl.Application.Contracts.Auth;
using HamzetWasl.Application.Interfaces;
using HamzetWasl.Domain.Constants;
using HamzetWasl.Domain.Entities;
using HamzetWasl.Domain.Rules;
using HamzetWasl.Infrastructure.Identity;
using HamzetWasl.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HamzetWasl.Infrastructure.Services;

public sealed class AuthService : IAuthService
{
    private const string RefreshProvider = "HamzetWasl";
    private const string EmailPurpose = "email";
    private const string PhonePurpose = "phone";

    private readonly HamzetWaslDbContext _db;
    private readonly UserManager<AppUser> _users;
    private readonly ITokenService _tokens;
    private readonly OtpService _otp;
    private readonly IEmailSender _email;
    private readonly ISmsSender _sms;
    private readonly IGoogleTokenValidator _google;
    private readonly IClock _clock;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        HamzetWaslDbContext db,
        UserManager<AppUser> users,
        ITokenService tokens,
        OtpService otp,
        IEmailSender email,
        ISmsSender sms,
        IGoogleTokenValidator google,
        IClock clock,
        ILogger<AuthService> logger)
    {
        _db = db;
        _users = users;
        _tokens = tokens;
        _otp = otp;
        _email = email;
        _sms = sms;
        _google = google;
        _clock = clock;
        _logger = logger;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var phone = PhoneNumberNormalizer.TryNormalize(request.Phone)
                    ?? throw AppException.Validation(new Dictionary<string, string[]> { ["phone"] = ["رقم الهاتف غير صحيح."] });

        if (!RoleNames.PublicRegistration.Contains(request.Role))
        {
            throw AppException.Validation(new Dictionary<string, string[]> { ["role"] = ["نوع الحساب غير صحيح."] });
        }

        if (await _db.UserAccounts.AnyAsync(a => a.Email == email, ct))
        {
            throw AppException.Conflict(ErrorCodes.EmailAlreadyExists);
        }

        if (await _db.UserAccounts.AnyAsync(a => a.Phone == phone, ct))
        {
            throw AppException.Conflict(ErrorCodes.PhoneAlreadyExists);
        }

        var role = await _db.AccountRoles.SingleOrDefaultAsync(r => r.RoleName == request.Role, ct)
                   ?? throw new AppException(500, ErrorCodes.InternalError, "تعذر تحميل نوع الحساب.");

        var now = _clock.UtcNow;
        var userId = Guid.NewGuid();
        var isOrganization = RoleNames.IsOrganization(request.Role);
        var displayName = isOrganization
            ? request.OrganizationName?.Trim()
            : request.Name?.Trim();

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw AppException.Validation(new Dictionary<string, string[]>
            {
                [isOrganization ? "organizationName" : "name"] = ["الاسم مطلوب."]
            });
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        var account = new UserAccount
        {
            UserId = userId,
            RoleId = role.RoleId,
            Email = email,
            EmailVerified = false,
            Phone = phone,
            PhoneVerified = false,
            AccountStatus = isOrganization ? AccountStatuses.PendingApproval : AccountStatuses.Active,
            CreatedAt = now
        };
        _db.UserAccounts.Add(account);

        _db.UserProfiles.Add(new UserProfile
        {
            UserId = userId,
            DisplayName = displayName,
            Visibility = ProfileVisibilities.Public
        });

        _db.ConsentRecords.Add(new ConsentRecord
        {
            ConsentId = Guid.NewGuid(),
            UserId = userId,
            Purpose = ConsentPurposes.Terms,
            PolicyVersion = string.IsNullOrWhiteSpace(request.PolicyVersion) ? "1.0" : request.PolicyVersion.Trim(),
            GrantedAt = now
        });

        if (request.Role == RoleNames.Volunteer)
        {
            _db.VolunteerProfiles.Add(new VolunteerProfile
            {
                UserId = userId,
                HelpMode = HelpModes.OnlineOnly,
                ServiceArea = string.Empty,
                SkillLevel = SkillLevels.Beginner,
                WeeklyHours = 1,
                VerificationStatus = VerificationStatuses.Unverified
            });
        }

        if (isOrganization)
        {
            _db.Organizations.Add(new Organization
            {
                OrganizationId = Guid.NewGuid(),
                OwnerUserId = userId,
                OrganizationType = request.Role,
                Name = displayName,
                ApprovalStatus = OrganizationApprovalStatuses.Pending
            });
        }

        await _db.SaveChangesAsync(ct);

        var appUser = new AppUser
        {
            Id = userId,
            UserName = email,
            Email = email,
            EmailConfirmed = false,
            PhoneNumber = phone,
            PhoneNumberConfirmed = false,
            UserAccount = account
        };

        var createResult = await _users.CreateAsync(appUser, request.Password);
        if (!createResult.Succeeded)
        {
            await transaction.RollbackAsync(ct);
            throw ToValidationException(createResult);
        }

        var response = await CreateSessionAsync(account, request.Role, displayName, null, ct);
        await transaction.CommitAsync(ct);
        return response;
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        AppUser? appUser;
        if (request.EmailOrPhone.Contains('@', StringComparison.Ordinal))
        {
            appUser = await _users.FindByEmailAsync(request.EmailOrPhone.Trim().ToLowerInvariant());
        }
        else
        {
            var phone = PhoneNumberNormalizer.TryNormalize(request.EmailOrPhone);
            if (phone is null) throw new AppException(401, ErrorCodes.InvalidCredentials);
            appUser = await _db.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phone, ct);
        }

        if (appUser is null || !await _users.CheckPasswordAsync(appUser, request.Password))
        {
            throw new AppException(401, ErrorCodes.InvalidCredentials);
        }

        var account = await GetAccountAsync(appUser.Id, ct);
        EnsureNotSuspended(account);
        var displayName = await GetDisplayNameAsync(appUser.Id, ct);
        return await CreateSessionAsync(account, account.Role.RoleName, displayName, null, ct);
    }

    public async Task<GoogleAuthResponse> GoogleAsync(GoogleAuthRequest request, CancellationToken ct = default)
    {
        var googleUser = await _google.ValidateAsync(request.IdToken, ct);
        if (googleUser is null || !googleUser.EmailVerified)
        {
            throw new AppException(401, ErrorCodes.GoogleTokenInvalid);
        }

        var email = googleUser.Email.Trim().ToLowerInvariant();
        var existing = await _db.UserAccounts.Include(a => a.Role)
            .FirstOrDefaultAsync(a => a.Email == email, ct);

        if (existing is not null)
        {
            EnsureNotSuspended(existing);
            var existingAppUser = await _users.FindByIdAsync(existing.UserId.ToString());
            if (existingAppUser is null) throw new AppException(401, ErrorCodes.GoogleTokenInvalid);

            if (!existing.EmailVerified)
            {
                existing.EmailVerified = true;
                existingAppUser.EmailConfirmed = true;
                await _db.SaveChangesAsync(ct);
            }

            var existingDisplayName = await GetDisplayNameAsync(existing.UserId, ct);
            var existingAuth = await CreateSessionAsync(existing, existing.Role.RoleName, existingDisplayName, null, ct);
            return new GoogleAuthResponse(existingAuth, null);
        }

        if (string.IsNullOrWhiteSpace(request.Role) || string.IsNullOrWhiteSpace(request.Phone) || !request.TermsAccepted)
        {
            var ticket = _tokens.CreateGoogleRegistrationTicket(email, googleUser.Name, googleUser.Subject);
            return new GoogleAuthResponse(null, new GoogleRegistrationRequiredResponse(
                true, ticket, email, googleUser.Name, RoleNames.PublicRegistration.ToArray()));
        }

        if (!RoleNames.PublicRegistration.Contains(request.Role))
        {
            throw AppException.Validation(new Dictionary<string, string[]> { ["role"] = ["نوع الحساب غير صحيح."] });
        }

        var phone = PhoneNumberNormalizer.TryNormalize(request.Phone)
                    ?? throw AppException.Validation(new Dictionary<string, string[]> { ["phone"] = ["رقم الهاتف غير صحيح."] });

        if (await _db.UserAccounts.AnyAsync(a => a.Phone == phone && a.Email != email, ct))
        {
            throw AppException.Conflict(ErrorCodes.PhoneAlreadyExists);
        }

        var role = await _db.AccountRoles.SingleOrDefaultAsync(r => r.RoleName == request.Role, ct)
                   ?? throw new AppException(500, ErrorCodes.InternalError);

        var now = _clock.UtcNow;
        var userId = Guid.NewGuid();
        var isOrganization = RoleNames.IsOrganization(request.Role);
        var displayName = string.IsNullOrWhiteSpace(googleUser.Name) ? email : googleUser.Name!;

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        var account = new UserAccount
        {
            UserId = userId,
            RoleId = role.RoleId,
            Email = email,
            EmailVerified = true,
            Phone = phone,
            PhoneVerified = false,
            AccountStatus = isOrganization ? AccountStatuses.PendingApproval : AccountStatuses.Active,
            CreatedAt = now
        };
        _db.UserAccounts.Add(account);
        _db.UserProfiles.Add(new UserProfile
        {
            UserId = userId,
            DisplayName = displayName,
            Visibility = ProfileVisibilities.Public
        });
        _db.ConsentRecords.Add(new ConsentRecord
        {
            ConsentId = Guid.NewGuid(),
            UserId = userId,
            Purpose = ConsentPurposes.Terms,
            PolicyVersion = string.IsNullOrWhiteSpace(request.PolicyVersion) ? "1.0" : request.PolicyVersion.Trim(),
            GrantedAt = now
        });

        if (request.Role == RoleNames.Volunteer)
        {
            _db.VolunteerProfiles.Add(new VolunteerProfile
            {
                UserId = userId,
                HelpMode = HelpModes.OnlineOnly,
                ServiceArea = string.Empty,
                SkillLevel = SkillLevels.Beginner,
                WeeklyHours = 1,
                VerificationStatus = VerificationStatuses.Unverified
            });
        }

        if (isOrganization)
        {
            _db.Organizations.Add(new Organization
            {
                OrganizationId = Guid.NewGuid(),
                OwnerUserId = userId,
                OrganizationType = request.Role,
                Name = displayName,
                ApprovalStatus = OrganizationApprovalStatuses.Pending
            });
        }

        await _db.SaveChangesAsync(ct);

        var appUser = new AppUser
        {
            Id = userId,
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            PhoneNumber = phone,
            PhoneNumberConfirmed = false,
            UserAccount = account
        };

        var createResult = await _users.CreateAsync(appUser);
        if (!createResult.Succeeded)
        {
            await transaction.RollbackAsync(ct);
            throw ToValidationException(createResult);
        }

        var auth = await CreateSessionAsync(account, request.Role, displayName, null, ct);
        await transaction.CommitAsync(ct);
        return new GoogleAuthResponse(auth, null);
    }

    public async Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken ct = default)
    {
        var payload = _tokens.ValidateRefreshToken(request.RefreshToken);
        if (payload is null) throw AppException.Unauthorized();

        var row = await _db.UserTokens.FirstOrDefaultAsync(t =>
            t.UserId == payload.UserId &&
            t.LoginProvider == RefreshProvider &&
            t.Name == $"refresh:{payload.TokenId}", ct);

        if (row is null || !FixedTimeEquals(row.Value, _tokens.HashRefreshToken(request.RefreshToken)))
        {
            throw AppException.Unauthorized();
        }

        var account = await GetAccountAsync(payload.UserId, ct);
        EnsureNotSuspended(account);
        var displayName = await GetDisplayNameAsync(payload.UserId, ct);
        return await CreateSessionAsync(account, account.Role.RoleName, displayName, row, ct);
    }

    public async Task LogoutAsync(Guid userId, string? refreshToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            await RevokeAllRefreshTokensAsync(userId, ct);
            return;
        }

        var payload = _tokens.ValidateRefreshToken(refreshToken);
        if (payload is null || payload.UserId != userId)
        {
            await RevokeAllRefreshTokensAsync(userId, ct);
            return;
        }

        await _db.UserTokens
            .Where(t => t.UserId == userId && t.LoginProvider == RefreshProvider && t.Name == $"refresh:{payload.TokenId}")
            .ExecuteDeleteAsync(ct);
    }

    public async Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct = default)
    {
        var appUser = await _users.FindByEmailAsync(request.Email.Trim().ToLowerInvariant());
        if (appUser is null) return;

        var token = await _users.GeneratePasswordResetTokenAsync(appUser);
        var body = $"""
            <p>لإعادة تعيين كلمة المرور، استخدم الرمز التالي:</p>
            <p dir="ltr"><strong>{System.Net.WebUtility.HtmlEncode(token)}</strong></p>
            <p>إذا لم تطلب ذلك، تجاهل هذه الرسالة.</p>
            """;
        await _email.SendAsync(appUser.Email!, "إعادة تعيين كلمة المرور - همزة وصل", body, ct);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default)
    {
        var appUser = await _users.FindByEmailAsync(request.Email.Trim().ToLowerInvariant());
        if (appUser is null) throw new AppException(400, ErrorCodes.OtpInvalid, "رمز إعادة التعيين غير صحيح.");

        var result = await _users.ResetPasswordAsync(appUser, request.Token, request.NewPassword);
        if (!result.Succeeded) throw ToValidationException(result);

        await RevokeAllRefreshTokensAsync(appUser.Id, ct);
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct = default)
    {
        var appUser = await _users.FindByIdAsync(userId.ToString());
        if (appUser is null) throw AppException.NotFound();

        var result = await _users.ChangePasswordAsync(appUser, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded) throw ToValidationException(result);

        await RevokeAllRefreshTokensAsync(userId, ct);
    }

    public async Task SendEmailCodeAsync(Guid userId, CancellationToken ct = default)
    {
        var account = await GetAccountAsync(userId, ct);
        if (account.EmailVerified) return;

        await _otp.EnsureCanSendAsync(userId, EmailPurpose, ct);
        var code = _otp.GenerateCode();
        var now = _clock.UtcNow;

        await _db.EmailVerifications
            .Where(v => v.UserId == userId && v.VerifiedAt == null)
            .ExecuteDeleteAsync(ct);

        _db.EmailVerifications.Add(new EmailVerification
        {
            VerificationId = Guid.NewGuid(),
            UserId = userId,
            CodeHash = _otp.Hash(userId, EmailPurpose, code),
            ExpiresAt = now.AddMinutes(_otp.ExpiryMinutes)
        });
        await _db.SaveChangesAsync(ct);

        await _email.SendAsync(
            account.Email,
            "رمز التحقق - همزة وصل",
            $"<p>رمز التحقق الخاص بك هو:</p><p dir=\"ltr\" style=\"font-size:24px\"><strong>{code}</strong></p><p>الرمز صالح لمدة {_otp.ExpiryMinutes} دقائق.</p>",
            ct);

        await _otp.MarkSentAsync(userId, EmailPurpose, ct);
    }

    public async Task VerifyEmailAsync(Guid userId, string code, CancellationToken ct = default)
    {
        var verification = await _db.EmailVerifications
            .Where(v => v.UserId == userId && v.VerifiedAt == null)
            .OrderByDescending(v => v.VerificationId)
            .FirstOrDefaultAsync(ct);

        await VerifyOtpAsync(verification?.ExpiresAt, verification?.CodeHash, userId, EmailPurpose, code, ct);

        verification!.VerifiedAt = _clock.UtcNow;
        var account = await GetAccountAsync(userId, ct);
        account.EmailVerified = true;
        var appUser = await _users.FindByIdAsync(userId.ToString());
        if (appUser is not null)
        {
            appUser.EmailConfirmed = true;
            await _users.UpdateAsync(appUser);
        }
        await _db.SaveChangesAsync(ct);
        await _otp.ResetAttemptsAsync(userId, EmailPurpose, ct);
    }

    public async Task SendPhoneCodeAsync(Guid userId, string phone, CancellationToken ct = default)
    {
        var normalized = PhoneNumberNormalizer.TryNormalize(phone)
                         ?? throw AppException.Validation(new Dictionary<string, string[]> { ["phone"] = ["رقم الهاتف غير صحيح."] });

        if (await _db.UserAccounts.AnyAsync(a => a.Phone == normalized && a.UserId != userId, ct))
        {
            throw AppException.Conflict(ErrorCodes.PhoneAlreadyExists);
        }

        var account = await GetAccountAsync(userId, ct);
        await _otp.EnsureCanSendAsync(userId, PhonePurpose, ct);

        if (account.Phone != normalized)
        {
            account.Phone = normalized;
            account.PhoneVerified = false;
            var appUser = await _users.FindByIdAsync(userId.ToString());
            if (appUser is not null)
            {
                appUser.PhoneNumber = normalized;
                appUser.PhoneNumberConfirmed = false;
                await _users.UpdateAsync(appUser);
            }
            await _db.SaveChangesAsync(ct);
        }

        var code = _otp.GenerateCode();
        var now = _clock.UtcNow;
        await _db.PhoneVerifications
            .Where(v => v.UserId == userId && v.VerifiedAt == null)
            .ExecuteDeleteAsync(ct);

        _db.PhoneVerifications.Add(new PhoneVerification
        {
            VerificationId = Guid.NewGuid(),
            UserId = userId,
            CodeHash = _otp.Hash(userId, PhonePurpose, code),
            ExpiresAt = now.AddMinutes(_otp.ExpiryMinutes)
        });
        await _db.SaveChangesAsync(ct);

        await _sms.SendAsync(normalized, $"رمز التحقق الخاص بك في همزة وصل: {code}", ct);
        await _otp.MarkSentAsync(userId, PhonePurpose, ct);
    }

    public async Task VerifyPhoneAsync(Guid userId, string code, CancellationToken ct = default)
    {
        var verification = await _db.PhoneVerifications
            .Where(v => v.UserId == userId && v.VerifiedAt == null)
            .OrderByDescending(v => v.VerificationId)
            .FirstOrDefaultAsync(ct);

        await VerifyOtpAsync(verification?.ExpiresAt, verification?.CodeHash, userId, PhonePurpose, code, ct);

        verification!.VerifiedAt = _clock.UtcNow;
        var account = await GetAccountAsync(userId, ct);
        account.PhoneVerified = true;
        var appUser = await _users.FindByIdAsync(userId.ToString());
        if (appUser is not null)
        {
            appUser.PhoneNumberConfirmed = true;
            await _users.UpdateAsync(appUser);
        }
        await _db.SaveChangesAsync(ct);
        await _otp.ResetAttemptsAsync(userId, PhonePurpose, ct);
    }

    public async Task<UserSessionDto> GetSessionAsync(Guid userId, CancellationToken ct = default)
    {
        var account = await GetAccountAsync(userId, ct);
        var displayName = await GetDisplayNameAsync(userId, ct);
        return new UserSessionDto(account.UserId, account.Role.RoleName, account.AccountStatus, account.Email,
            account.EmailVerified, account.Phone, account.PhoneVerified, displayName);
    }

    private async Task<AuthResponse> CreateSessionAsync(
        UserAccount account,
        string roleName,
        string? displayName,
        IdentityUserToken<Guid>? tokenToRemove,
        CancellationToken ct)
    {
        var session = new UserSessionDto(account.UserId, roleName, account.AccountStatus, account.Email,
            account.EmailVerified, account.Phone, account.PhoneVerified, displayName);
        var access = _tokens.CreateAccessToken(session);
        var refresh = _tokens.CreateRefreshToken(account.UserId);

        _db.UserTokens.Add(new IdentityUserToken<Guid>
        {
            UserId = account.UserId,
            LoginProvider = RefreshProvider,
            Name = $"refresh:{refresh.TokenId}",
            Value = _tokens.HashRefreshToken(refresh.Token)
        });

        if (tokenToRemove is not null)
        {
            _db.UserTokens.Remove(tokenToRemove);
        }

        await _db.SaveChangesAsync(ct);

        return new AuthResponse(
            account.UserId,
            roleName,
            account.AccountStatus,
            account.Email,
            account.EmailVerified,
            account.Phone,
            account.PhoneVerified,
            displayName,
            access.Token,
            access.ExpiresAt,
            refresh.Token,
            refresh.ExpiresAt);
    }

    private async Task VerifyOtpAsync(
        DateTime? expiresAt,
        string? codeHash,
        Guid userId,
        string purpose,
        string code,
        CancellationToken ct)
    {
        if (expiresAt is null || codeHash is null || expiresAt < _clock.UtcNow)
        {
            throw new AppException(422, ErrorCodes.OtpExpired);
        }

        var attempts = await _otp.GetAttemptsAsync(userId, purpose, ct);
        if (attempts >= _otp.MaxAttempts)
        {
            throw new AppException(422, ErrorCodes.OtpTooManyAttempts);
        }

        if (!_otp.Verify(userId, purpose, code, codeHash))
        {
            var remaining = await _otp.IncrementAttemptsAsync(userId, purpose, ct);
            var left = Math.Max(0, _otp.MaxAttempts - remaining);
            throw new AppException(422, ErrorCodes.OtpInvalid,
                $"{ArabicMessages.For(ErrorCodes.OtpInvalid)} المحاولات المتبقية: {left}");
        }
    }

    private async Task<UserAccount> GetAccountAsync(Guid userId, CancellationToken ct)
    {
        return await _db.UserAccounts
                   .Include(a => a.Role)
                   .FirstOrDefaultAsync(a => a.UserId == userId, ct)
               ?? throw AppException.NotFound();
    }

    private async Task<string?> GetDisplayNameAsync(Guid userId, CancellationToken ct)
    {
        var profile = await _db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (!string.IsNullOrWhiteSpace(profile?.DisplayName)) return profile.DisplayName;

        var org = await _db.Organizations.FirstOrDefaultAsync(o => o.OwnerUserId == userId, ct);
        return org?.Name;
    }

    private static void EnsureNotSuspended(UserAccount account)
    {
        if (account.AccountStatus == AccountStatuses.Suspended)
        {
            throw AppException.Forbidden(ErrorCodes.AccountSuspended);
        }
    }

    private async Task RevokeAllRefreshTokensAsync(Guid userId, CancellationToken ct)
    {
        await _db.UserTokens
            .Where(t => t.UserId == userId && t.LoginProvider == RefreshProvider)
            .ExecuteDeleteAsync(ct);
    }

    private static bool FixedTimeEquals(string? a, string? b)
    {
        if (a is null || b is null) return false;
        var left = System.Text.Encoding.UTF8.GetBytes(a);
        var right = System.Text.Encoding.UTF8.GetBytes(b);
        return left.Length == right.Length && System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(left, right);
    }

    private static AppException ToValidationException(IdentityResult result)
    {
        var errors = result.Errors.Select(e => e.Description).Distinct().ToArray();
        var fields = new Dictionary<string, string[]>();
        if (errors.Any(e => e.Contains("password", StringComparison.OrdinalIgnoreCase) ||
                            e.Contains("Passwords", StringComparison.OrdinalIgnoreCase)))
        {
            fields["password"] = ["كلمة المرور غير صالحة."];
        }
        else
        {
            fields["email"] = errors.Length == 0 ? ["تعذر إنشاء الحساب."] : errors;
        }

        return AppException.Validation(fields);
    }
}
