using HamzetWasl.Application.Contracts.Profile;

namespace HamzetWasl.Application.Interfaces;

/// <summary>
/// Creates the account-deletion request that the admin queue resolves.
/// The request only asks for deletion; the account stays usable until an admin
/// approves it and the retention job completes the anonymisation.
/// </summary>
public interface IDeletionRequestService
{
    /// <summary>
    /// Stores a <c>pending</c> row in <c>DELETION_REQUEST</c>. One pending request
    /// per user is allowed; a second call fails with 409.
    /// </summary>
    Task<DeletionRequestResponse> RequestAsync(Guid userId, CancellationToken ct = default);
}
