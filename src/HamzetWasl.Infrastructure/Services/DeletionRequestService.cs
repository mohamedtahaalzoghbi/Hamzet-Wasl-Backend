using HamzetWasl.Application.Common;
using HamzetWasl.Application.Contracts.Profile;
using HamzetWasl.Application.Interfaces;
using HamzetWasl.Domain.Constants;
using HamzetWasl.Domain.Entities;
using HamzetWasl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HamzetWasl.Infrastructure.Services;

/// <summary>
/// Writes the requester-side half of the account deletion flow. The admin side
/// (<c>AdminService.GetDeletionRequestsAsync</c> / <c>ResolveDeletionRequestAsync</c>)
/// already reads and resolves these rows.
/// </summary>
public sealed class DeletionRequestService(
    HamzetWaslDbContext db,
    IClock clock) : IDeletionRequestService
{
    public async Task<DeletionRequestResponse> RequestAsync(Guid userId, CancellationToken ct = default)
    {
        // One pending request per user: the mobile screen can be retried safely
        // and the admin queue never contains duplicates for the same account.
        var hasPending = await db.DeletionRequests
            .AnyAsync(r => r.UserId == userId && r.Status == DeletionRequestStatuses.Pending, ct);

        if (hasPending)
        {
            throw AppException.Conflict(ErrorCodes.DeletionRequestAlreadyPending);
        }

        var request = new DeletionRequest
        {
            // The schema default is NEWSEQUENTIALID; assigning explicitly keeps this
            // service consistent with the other services that create rows.
            DeletionRequestId = Guid.NewGuid(),
            UserId = userId,
            Status = DeletionRequestStatuses.Pending,
            RequestedAt = clock.UtcNow
        };

        db.DeletionRequests.Add(request);
        await db.SaveChangesAsync(ct);

        return new DeletionRequestResponse(
            request.DeletionRequestId,
            request.Status,
            request.RequestedAt);
    }
}
