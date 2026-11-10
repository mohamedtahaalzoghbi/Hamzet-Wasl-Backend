using HamzetWasl.Application.Contracts.Organization;

namespace HamzetWasl.Application.Interfaces;

public interface IOrganizationService
{
    Task<OrganizationResponse> GetAsync(Guid userId, CancellationToken ct = default);
    Task<OrganizationResponse> UpdateAsync(Guid userId, UpdateOrganizationRequest request, CancellationToken ct = default);
    Task<string> UpdateLogoAsync(Guid userId, Common.FileUpload file, CancellationToken ct = default);
    Task<string> UpdateCoverAsync(Guid userId, Common.FileUpload file, CancellationToken ct = default);
    Task<OrganizationResponse> UploadDocumentAsync(Guid userId, string documentType, Common.FileUpload file, CancellationToken ct = default);
    Task<SubmitOrganizationResponse> SubmitAsync(Guid userId, CancellationToken ct = default);
    Task<OrganizationReviewStatusResponse> GetReviewStatusAsync(Guid userId, CancellationToken ct = default);
}
