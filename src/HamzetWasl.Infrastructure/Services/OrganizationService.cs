using HamzetWasl.Application.Common;
using HamzetWasl.Application.Contracts.Meta;
using HamzetWasl.Application.Contracts.Organization;
using HamzetWasl.Application.Interfaces;
using HamzetWasl.Domain.Constants;
using HamzetWasl.Domain.Entities;
using HamzetWasl.Domain.Rules;
using HamzetWasl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HamzetWasl.Infrastructure.Services;

public sealed class OrganizationService : IOrganizationService
{
    private readonly HamzetWaslDbContext _db;
    private readonly IFileStorage _storage;
    private readonly IGeoCatalog _geo;
    private readonly IClock _clock;

    public OrganizationService(HamzetWaslDbContext db, IFileStorage storage, IGeoCatalog geo, IClock clock)
    {
        _db = db;
        _storage = storage;
        _geo = geo;
        _clock = clock;
    }

    public async Task<OrganizationResponse> GetAsync(Guid userId, CancellationToken ct = default)
    {
        var organization = await GetOrganizationAsync(userId, ct);
        return Map(organization);
    }

    public async Task<OrganizationResponse> UpdateAsync(Guid userId, UpdateOrganizationRequest request, CancellationToken ct = default)
    {
        var organization = await GetOrganizationAsync(userId, ct);

        // التحقق من الكتالوج الديناميكي (GOVERNORATE/AREA) بدلاً من MetaCatalog الثابت.
        var governorate = await _geo.FindGovernorateAsync(request.Governorate, ct);
        if (!string.IsNullOrWhiteSpace(request.Governorate) && governorate is null)
        {
            throw AppException.Validation(new Dictionary<string, string[]> { ["governorate"] = ["المحافظة غير صحيحة."] });
        }

        AreaDto? area = null;
        if (!string.IsNullOrWhiteSpace(request.City) && governorate is not null)
        {
            area = await _geo.FindAreaAsync(governorate, request.City, ct);
            if (area is null)
            {
                throw AppException.Validation(new Dictionary<string, string[]> { ["city"] = ["المنطقة غير صحيحة."] });
            }
        }

        organization.Name = request.Name.Trim();
        organization.Field = NormalizeField(request.Field);
        organization.Description = Clean(request.Description);
        organization.Governorate = governorate?.Key;
        organization.City = area?.Key;
        organization.Website = Clean(request.Website);
        organization.EmployeeCountRange = NormalizeEmployeeRange(request.EmployeeCountRange);
        organization.ContactInformation = Clean(request.ContactInformation);

        await _db.SaveChangesAsync(ct);
        return Map(organization);
    }

    public async Task<string> UpdateLogoAsync(Guid userId, FileUpload file, CancellationToken ct = default)
    {
        var organization = await GetOrganizationAsync(userId, ct);
        var stored = await _storage.SaveAsync(file, FileCategory.PublicImage, userId, ct);
        var old = organization.LogoStorageKey;
        organization.LogoStorageKey = stored.Key;
        await _db.SaveChangesAsync(ct);
        if (!string.IsNullOrWhiteSpace(old)) await _storage.DeleteAsync(old, ct);
        return _storage.GetPublicUrl(stored.Key);
    }

    public async Task<string> UpdateCoverAsync(Guid userId, FileUpload file, CancellationToken ct = default)
    {
        var organization = await GetOrganizationAsync(userId, ct);
        var stored = await _storage.SaveAsync(file, FileCategory.PublicImage, userId, ct);
        var old = organization.CoverStorageKey;
        organization.CoverStorageKey = stored.Key;
        await _db.SaveChangesAsync(ct);
        if (!string.IsNullOrWhiteSpace(old)) await _storage.DeleteAsync(old, ct);
        return _storage.GetPublicUrl(stored.Key);
    }

    public async Task<OrganizationResponse> UploadDocumentAsync(Guid userId, string documentType, FileUpload file, CancellationToken ct = default)
    {
        if (!DocumentTypes.All.Contains(documentType))
        {
            throw new AppException(400, ErrorCodes.DocumentTypeInvalid);
        }

        var organization = await GetOrganizationAsync(userId, ct);
        var stored = await _storage.SaveAsync(file, FileCategory.OrganizationDocument, organization.OrganizationId, ct);

        var existing = organization.OrganizationDocuments.FirstOrDefault(d => d.DocumentType == documentType);
        if (existing is null)
        {
            var document = new OrganizationDocument
            {
                DocumentId = Guid.NewGuid(),
                OrganizationId = organization.OrganizationId,
                DocumentType = documentType,
                StorageKey = stored.Key,
                UploadedAt = _clock.UtcNow
            };
            _db.OrganizationDocuments.Add(document);
            organization.OrganizationDocuments.Add(document);
        }
        else
        {
            var old = existing.StorageKey;
            existing.StorageKey = stored.Key;
            existing.UploadedAt = _clock.UtcNow;
            if (!string.IsNullOrWhiteSpace(old)) await _storage.DeleteAsync(old, ct);
        }

        await _db.SaveChangesAsync(ct);
        return Map(organization);
    }

    public async Task<SubmitOrganizationResponse> SubmitAsync(Guid userId, CancellationToken ct = default)
    {
        var organization = await GetOrganizationAsync(userId, ct);
        if (organization.ApprovalStatus == OrganizationApprovalStatuses.Approved)
        {
            throw new AppException(422, ErrorCodes.InvalidState, "تم اعتماد المؤسسة بالفعل.");
        }

        var missing = MissingDocuments(organization);
        if (missing.Count > 0)
        {
            throw new AppException(422, ErrorCodes.DocumentsMissing,
                message: $"{ArabicMessages.For(ErrorCodes.DocumentsMissing)} ({string.Join(", ", missing)})");
        }

        var missingProfile = MissingProfileFields(organization);
        if (missingProfile.Count > 0)
        {
            throw new AppException(422, ErrorCodes.ProfileIncomplete,
                message: $"{ArabicMessages.For(ErrorCodes.ProfileIncomplete)} ({string.Join(", ", missingProfile)})");
        }

        var latest = organization.OrganizationReviews.OrderByDescending(r => r.RequestedAt).FirstOrDefault();
        if (latest?.Status == OrganizationReviewStatuses.Pending)
        {
            throw new AppException(409, ErrorCodes.OrganizationAlreadySubmitted);
        }

        var now = _clock.UtcNow;
        var review = new OrganizationReview
        {
            ReviewId = Guid.NewGuid(),
            OrganizationId = organization.OrganizationId,
            Status = OrganizationReviewStatuses.Pending,
            RequestedAt = now
        };
        _db.OrganizationReviews.Add(review);
        organization.ApprovalStatus = OrganizationApprovalStatuses.Pending;
        await _db.SaveChangesAsync(ct);

        return new SubmitOrganizationResponse(review.ReviewId, organization.ApprovalStatus, review.Status, review.RequestedAt);
    }

    public async Task<OrganizationReviewStatusResponse> GetReviewStatusAsync(Guid userId, CancellationToken ct = default)
    {
        var organization = await GetOrganizationAsync(userId, ct);
        var latest = organization.OrganizationReviews.OrderByDescending(r => r.RequestedAt).FirstOrDefault();
        var missing = MissingDocuments(organization);
        var canSubmit = missing.Count == 0 &&
                        MissingProfileFields(organization).Count == 0 &&
                        organization.ApprovalStatus != OrganizationApprovalStatuses.Approved &&
                        latest?.Status != OrganizationReviewStatuses.Pending;

        return new OrganizationReviewStatusResponse(
            organization.ApprovalStatus,
            latest?.ReviewId,
            latest?.Status,
            latest?.RequestedAt,
            latest?.DecidedAt,
            latest?.DecisionNotes,
            missing,
            canSubmit);
    }

    private async Task<Organization> GetOrganizationAsync(Guid userId, CancellationToken ct)
    {
        var organization = await _db.Organizations
            .Include(o => o.OwnerUser).ThenInclude(u => u.Role)
            .Include(o => o.OrganizationDocuments)
            .Include(o => o.OrganizationReviews)
            .FirstOrDefaultAsync(o => o.OwnerUserId == userId, ct)
            ?? throw AppException.NotFound("لم يتم إنشاء ملف المؤسسة بعد.");

        var role = organization.OwnerUser.Role.RoleName;
        if (!RoleNames.IsOrganization(role))
        {
            throw AppException.Forbidden(ErrorCodes.Forbidden, "هذا الحساب ليس حساب مؤسسة.");
        }

        if (!BusinessRules.OrganizationTypeMatchesRole(role, organization.OrganizationType))
        {
            throw AppException.Forbidden(ErrorCodes.Forbidden, "نوع المؤسسة لا يطابق نوع الحساب.");
        }

        return organization;
    }

    private OrganizationResponse Map(Organization organization)
    {
        var missing = MissingDocuments(organization);
        var latest = organization.OrganizationReviews.OrderByDescending(r => r.RequestedAt).FirstOrDefault();
        var canSubmit = missing.Count == 0 &&
                        MissingProfileFields(organization).Count == 0 &&
                        organization.ApprovalStatus != OrganizationApprovalStatuses.Approved &&
                        latest?.Status != OrganizationReviewStatuses.Pending;

        var completion = 0;
        var total = 12;
        if (!string.IsNullOrWhiteSpace(organization.Name)) completion++;
        if (!string.IsNullOrWhiteSpace(organization.Field)) completion++;
        if (!string.IsNullOrWhiteSpace(organization.Description)) completion++;
        if (!string.IsNullOrWhiteSpace(organization.Governorate)) completion++;
        if (!string.IsNullOrWhiteSpace(organization.City)) completion++;
        if (!string.IsNullOrWhiteSpace(organization.LogoStorageKey)) completion++;
        if (!string.IsNullOrWhiteSpace(organization.CoverStorageKey)) completion++;
        if (!string.IsNullOrWhiteSpace(organization.EmployeeCountRange)) completion++;
        if (!string.IsNullOrWhiteSpace(organization.ContactInformation)) completion++;
        completion += DocumentTypes.Required.Count(t => organization.OrganizationDocuments.Any(d => d.DocumentType == t));

        return new OrganizationResponse(
            organization.OrganizationId,
            organization.OrganizationType,
            organization.Name,
            organization.Field,
            organization.Description,
            organization.Governorate,
            organization.City,
            organization.Website,
            string.IsNullOrWhiteSpace(organization.LogoStorageKey) ? null : _storage.GetPublicUrl(organization.LogoStorageKey),
            string.IsNullOrWhiteSpace(organization.CoverStorageKey) ? null : _storage.GetPublicUrl(organization.CoverStorageKey),
            organization.EmployeeCountRange,
            organization.ContactInformation,
            organization.ApprovalStatus,
            organization.OrganizationDocuments.OrderBy(d => d.DocumentType)
                .Select(d => new OrganizationDocumentDto(d.DocumentType, d.UploadedAt)).ToList(),
            missing,
            canSubmit,
            (int)Math.Round(completion * 100.0 / total));
    }

    private static List<string> MissingDocuments(Organization organization) =>
        DocumentTypes.Required.Where(type => organization.OrganizationDocuments.All(d => d.DocumentType != type)).ToList();

    private static List<string> MissingProfileFields(Organization organization)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(organization.Name)) missing.Add("name");
        if (string.IsNullOrWhiteSpace(organization.Field)) missing.Add("field");
        if (string.IsNullOrWhiteSpace(organization.Description)) missing.Add("description");
        if (string.IsNullOrWhiteSpace(organization.Governorate)) missing.Add("governorate");
        if (string.IsNullOrWhiteSpace(organization.City)) missing.Add("city");
        if (string.IsNullOrWhiteSpace(organization.EmployeeCountRange)) missing.Add("employeeCountRange");
        if (string.IsNullOrWhiteSpace(organization.ContactInformation)) missing.Add("contactInformation");
        if (string.IsNullOrWhiteSpace(organization.LogoStorageKey)) missing.Add("logo");
        return missing;
    }

    private static string? NormalizeField(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();

    private static string? NormalizeEmployeeRange(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
