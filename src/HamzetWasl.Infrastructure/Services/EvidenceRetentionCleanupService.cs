using HamzetWasl.Application.Interfaces;
using HamzetWasl.Domain.Constants;
using HamzetWasl.Infrastructure.Configuration;
using HamzetWasl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HamzetWasl.Infrastructure.Services;

/// <summary>
/// Applies the documented retention policy (OPEN_QUESTIONS Q13): verification evidence is
/// kept for <see cref="RetentionOptions.VerificationEvidenceDays"/> after the review
/// decision, then the encrypted file and its row are deleted. Evidence attached to an
/// open review round (draft, pending, interview scheduled) is never touched, so a
/// re-application that is still in progress keeps its documents.
/// </summary>
public sealed class EvidenceRetentionCleanupService(
    IServiceScopeFactory scopeFactory,
    IOptions<RetentionOptions> retention,
    ILogger<EvidenceRetentionCleanupService> logger) : BackgroundService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(5);
    private const int BatchSize = 100;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromHours(Math.Max(1, retention.Value.CleanupIntervalHours));

        try
        {
            await Task.Delay(InitialDelay, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Verification evidence retention cleanup failed; the next run retries.");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task CleanupOnceAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HamzetWaslDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<IFileStorage>();
        var protector = scope.ServiceProvider.GetRequiredService<StorageKeyProtector>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        string[] closedStatuses =
        [
            VerificationReviewStatuses.Approved,
            VerificationReviewStatuses.Rejected,
            VerificationReviewStatuses.Cancelled
        ];

        while (!ct.IsCancellationRequested)
        {
            var expired = await db.VerificationEvidences
                .Where(e => e.RetentionUntil <= clock.UtcNow && closedStatuses.Contains(e.Review.Status))
                .OrderBy(e => e.EvidenceId)
                .Take(BatchSize)
                .Select(e => new { e.EvidenceId, e.EncryptedStorageKey })
                .ToListAsync(ct);

            if (expired.Count == 0)
            {
                return;
            }

            // Only a file that was actually deleted loses its database row. A failed
            // delete keeps the row, so the next run retries it: nothing is orphaned.
            var deletedIds = new List<Guid>(expired.Count);
            foreach (var item in expired)
            {
                try
                {
                    await storage.DeleteAsync(protector.Unprotect(item.EncryptedStorageKey), ct);
                    deletedIds.Add(item.EvidenceId);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to delete the expired evidence file {EvidenceId}; its row is kept for the next run.", item.EvidenceId);
                }
            }

            if (deletedIds.Count == 0)
            {
                return;
            }

            var removed = await db.VerificationEvidences
                .Where(e => deletedIds.Contains(e.EvidenceId))
                .ExecuteDeleteAsync(ct);

            logger.LogInformation("Retention cleanup removed {Count} expired verification evidence record(s).", removed);

            if (deletedIds.Count < expired.Count)
            {
                // Some files could not be deleted; they retry on the next scheduled run
                // instead of a hot loop over the same failing rows.
                return;
            }
        }
    }
}
