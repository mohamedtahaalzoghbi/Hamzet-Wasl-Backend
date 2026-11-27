using HamzetWasl.Application.Common;
using HamzetWasl.Application.Contracts.Social;
using HamzetWasl.Application.Interfaces;
using HamzetWasl.Domain.Entities;
using HamzetWasl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HamzetWasl.Infrastructure.Services;

public sealed class SocialService(HamzetWaslDbContext db, IFileStorage storage, IClock clock) : ISocialService
{
    private const string Published = "approved";
    private const string Hidden = "hidden";
    private const int MaxMediaPerPost = 4;
    private static readonly string[] Reactions = ["like", "support", "thanks"];
    private static readonly string[] MemberRoles = ["disabled_user", "volunteer", "association", "company", "shop"];

    private async Task<string> RoleOfAsync(Guid userId, CancellationToken ct) =>
        await db.UserAccounts.Where(u => u.UserId == userId).Select(u => u.Role.RoleName).FirstOrDefaultAsync(ct) ?? "";

    private static bool CanSee(string viewerRole, string? audienceRole) =>
        audienceRole is null || viewerRole == "admin" || viewerRole == audienceRole;

    /// <summary>Throws 404 when the community does not exist or the caller's role may not see it.</summary>
    private async Task<Community> RequireVisibleCommunityAsync(Guid userId, Guid communityId, CancellationToken ct)
    {
        var community = await db.Communities.FirstOrDefaultAsync(c => c.CommunityId == communityId, ct) ?? throw AppException.NotFound();
        if (!CanSee(await RoleOfAsync(userId, ct), community.AudienceRole)) throw AppException.NotFound();
        return community;
    }

    private static AppException Invalid(string field, string message) =>
        AppException.Validation(new Dictionary<string, string[]> { [field] = [message] });

    private static (int, int) Clamp(int page, int pageSize) => (Math.Max(1, page), Math.Clamp(pageSize, 1, 50));

    private async Task<Dictionary<Guid, string>> NamesAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        return await db.UserProfiles.Where(p => list.Contains(p.UserId)).ToDictionaryAsync(p => p.UserId, p => p.DisplayName, ct);
    }

    public async Task<IReadOnlyList<CommunityDto>> GetCommunitiesAsync(Guid userId, CancellationToken ct = default)
    {
        var viewerRole = await RoleOfAsync(userId, ct);
        var rows = await db.Communities.OrderBy(c => c.Name).ToListAsync(ct);
        rows = rows.Where(c => CanSee(viewerRole, c.AudienceRole)).ToList();
        var ids = rows.Select(c => c.CommunityId).ToList();
        var members = await db.CommunityMemberships.Where(m => ids.Contains(m.CommunityId)).GroupBy(m => m.CommunityId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N, ct);
        var posts = await db.Posts.Where(p => ids.Contains(p.CommunityId) && p.ModerationStatus == Published).GroupBy(p => p.CommunityId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N, ct);
        var mine = (await db.CommunityMemberships.Where(m => m.UserId == userId).Select(m => m.CommunityId).ToListAsync(ct)).ToHashSet();
        return rows.Select(c => new CommunityDto(c.CommunityId, c.Name, c.Description, c.Rules, members.GetValueOrDefault(c.CommunityId), posts.GetValueOrDefault(c.CommunityId), mine.Contains(c.CommunityId), c.AudienceRole)).ToList();
    }

    public async Task<CommunityDto> CreateCommunityAsync(Guid adminId, CreateCommunityRequest r, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(r.Name) || r.Name.Length > 200) throw Invalid("name", "اسم المجتمع مطلوب (حتى 200 حرف).");
        if (string.IsNullOrWhiteSpace(r.Description)) throw Invalid("description", "الوصف مطلوب.");
        if (string.IsNullOrWhiteSpace(r.Rules)) throw Invalid("rules", "قواعد المجتمع مطلوبة.");
        var audienceRole = string.IsNullOrWhiteSpace(r.AudienceRole) ? null : r.AudienceRole.Trim();
        if (audienceRole is not null && !MemberRoles.Contains(audienceRole)) throw Invalid("audienceRole", "نوع الجمهور غير صحيح.");
        if (await db.Communities.AnyAsync(c => c.Name == r.Name.Trim(), ct)) throw AppException.Conflict(message: "فيه مجتمع بنفس الاسم.");
        var c = new Community { CommunityId = Guid.NewGuid(), Name = r.Name.Trim(), Description = r.Description.Trim(), Rules = r.Rules.Trim(), AudienceRole = audienceRole };
        db.Communities.Add(c);
        await db.SaveChangesAsync(ct);
        return new CommunityDto(c.CommunityId, c.Name, c.Description, c.Rules, 0, 0, false, c.AudienceRole);
    }

    public async Task JoinAsync(Guid userId, Guid communityId, CancellationToken ct = default)
    {
        var community = await RequireVisibleCommunityAsync(userId, communityId, ct);
        var role = await RoleOfAsync(userId, ct);
        if (community.AudienceRole is not null && role != community.AudienceRole)
            throw AppException.Forbidden(message: "المجتمع ده مخصوص لنوع حساب معين.");
        if (await db.CommunityMemberships.AnyAsync(m => m.CommunityId == communityId && m.UserId == userId, ct)) return;
        db.CommunityMemberships.Add(new CommunityMembership { CommunityId = communityId, UserId = userId, JoinedAt = clock.UtcNow });
        await db.SaveChangesAsync(ct);
    }

    public async Task LeaveAsync(Guid userId, Guid communityId, CancellationToken ct = default)
    {
        var m = await db.CommunityMemberships.FirstOrDefaultAsync(x => x.CommunityId == communityId && x.UserId == userId, ct);
        if (m is null) return;
        db.CommunityMemberships.Remove(m);
        await db.SaveChangesAsync(ct);
    }

    public async Task<PagedResult<PostDto>> GetFeedAsync(Guid userId, Guid? communityId, Guid? authorId, bool followingOnly, int page, int pageSize, CancellationToken ct = default)
    {
        (page, pageSize) = Clamp(page, pageSize);
        IQueryable<Post> q = db.Posts.Where(p => p.ModerationStatus == Published);
        var viewerRole = await RoleOfAsync(userId, ct);
        if (viewerRole != "admin")
        {
            q = q.Where(p => p.Community.AudienceRole == null || p.Community.AudienceRole == viewerRole);
        }
        if (communityId is { } cid) q = q.Where(p => p.CommunityId == cid);
        if (authorId is { } aid) q = q.Where(p => p.AuthorId == aid);
        if (followingOnly)
        {
            var followed = db.UserFollows.Where(f => f.FollowerId == userId).Select(f => f.FollowedId);
            q = q.Where(p => followed.Contains(p.AuthorId) || p.AuthorId == userId);
        }
        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(p => p.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<PostDto>(await ToDtosAsync(rows, userId, ct), page, pageSize, total);
    }

    public async Task<PostDto> CreatePostAsync(Guid userId, CreatePostRequest r, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(r.Body)) throw Invalid("body", "اكتب نص البوست.");
        if (r.Body.Length > 3000) throw Invalid("body", "البوست طويل (حتى 3000 حرف).");
        if (!await db.Communities.AnyAsync(c => c.CommunityId == r.CommunityId, ct)) throw AppException.NotFound();
        if (!await db.CommunityMemberships.AnyAsync(m => m.CommunityId == r.CommunityId && m.UserId == userId, ct))
            throw AppException.Forbidden(message: "انضم للمجتمع الأول عشان تنشر فيه.");
        var p = new Post { PostId = Guid.NewGuid(), AuthorId = userId, CommunityId = r.CommunityId, Body = r.Body.Trim(), ModerationStatus = Published, CreatedAt = clock.UtcNow };
        db.Posts.Add(p);
        await db.SaveChangesAsync(ct);
        return (await ToDtosAsync([p], userId, ct))[0];
    }

    public async Task<PostMediaDto> AddPostMediaAsync(Guid userId, Guid postId, string altText, FileUpload file, CancellationToken ct = default)
    {
        // The POST_MEDIA check constraint forces alternative text on every image; it is
        // what screen readers read, so it is required at the API level as well.
        if (string.IsNullOrWhiteSpace(altText)) throw Invalid("altText", "اكتب وصف للصورة (نص بديل).");
        var post = await db.Posts.FirstOrDefaultAsync(x => x.PostId == postId && x.ModerationStatus == Published, ct) ?? throw AppException.NotFound();
        if (post.AuthorId != userId) throw AppException.Forbidden(message: "صاحب البوست بس اللي يقدر يضيف صور.");
        var count = await db.PostMedia.CountAsync(m => m.PostId == postId, ct);
        if (count >= MaxMediaPerPost) throw Invalid("file", $"أقصى عدد صور للبوست {MaxMediaPerPost}.");

        var stored = await storage.SaveAsync(file, FileCategory.PublicImage, userId, ct);
        var media = new PostMedium
        {
            MediaId = Guid.NewGuid(),
            PostId = postId,
            MediaType = "image",
            StorageKey = stored.Key,
            AlternativeText = altText.Trim()
        };
        db.PostMedia.Add(media);
        await db.SaveChangesAsync(ct);
        return new PostMediaDto(media.MediaId, storage.GetPublicUrl(stored.Key), media.AlternativeText);
    }

    public async Task<PostDetailDto> GetPostAsync(Guid userId, Guid postId, CancellationToken ct = default)
    {
        var p = await db.Posts.FirstOrDefaultAsync(x => x.PostId == postId && x.ModerationStatus == Published, ct) ?? throw AppException.NotFound();
        await RequireVisibleCommunityAsync(userId, p.CommunityId, ct);
        var comments = await db.Comments.Where(c => c.PostId == postId && c.ModerationStatus == Published).OrderBy(c => c.CreatedAt).Take(200).ToListAsync(ct);
        var names = await NamesAsync(comments.Select(c => c.AuthorId), ct);
        return new PostDetailDto((await ToDtosAsync([p], userId, ct))[0],
            comments.Select(c => new CommentDto(c.CommentId, c.AuthorId, names.GetValueOrDefault(c.AuthorId, "مستخدم"), c.Body, c.CreatedAt)).ToList());
    }

    public async Task DeletePostAsync(Guid userId, bool isAdmin, Guid postId, CancellationToken ct = default)
    {
        var p = await db.Posts.FirstOrDefaultAsync(x => x.PostId == postId, ct) ?? throw AppException.NotFound();
        if (p.AuthorId != userId && !isAdmin) throw AppException.Forbidden();
        p.ModerationStatus = Hidden;
        await db.SaveChangesAsync(ct);
    }

    public async Task<CommentDto> AddCommentAsync(Guid userId, Guid postId, string body, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(body)) throw Invalid("body", "اكتب التعليق.");
        if (body.Length > 1000) throw Invalid("body", "التعليق طويل (حتى 1000 حرف).");
        var commentedPost = await db.Posts.FirstOrDefaultAsync(x => x.PostId == postId && x.ModerationStatus == Published, ct) ?? throw AppException.NotFound();
        await RequireVisibleCommunityAsync(userId, commentedPost.CommunityId, ct);
        var c = new Comment { CommentId = Guid.NewGuid(), PostId = postId, AuthorId = userId, Body = body.Trim(), ModerationStatus = Published, CreatedAt = clock.UtcNow };
        db.Comments.Add(c);
        await db.SaveChangesAsync(ct);
        var names = await NamesAsync([userId], ct);
        return new CommentDto(c.CommentId, userId, names.GetValueOrDefault(userId, "مستخدم"), c.Body, c.CreatedAt);
    }

    public async Task ReactAsync(Guid userId, Guid postId, string type, CancellationToken ct = default)
    {
        if (!Reactions.Contains(type)) throw Invalid("type", "نوع التفاعل غير صحيح (like أو support أو thanks).");
        var reactedPost = await db.Posts.FirstOrDefaultAsync(x => x.PostId == postId && x.ModerationStatus == Published, ct) ?? throw AppException.NotFound();
        await RequireVisibleCommunityAsync(userId, reactedPost.CommunityId, ct);
        var r = await db.PostReactions.FirstOrDefaultAsync(x => x.PostId == postId && x.UserId == userId, ct);
        if (r is null) db.PostReactions.Add(new PostReaction { PostId = postId, UserId = userId, ReactionType = type });
        else r.ReactionType = type;
        await db.SaveChangesAsync(ct);
    }

    public async Task UnreactAsync(Guid userId, Guid postId, CancellationToken ct = default)
    {
        var r = await db.PostReactions.FirstOrDefaultAsync(x => x.PostId == postId && x.UserId == userId, ct);
        if (r is null) return;
        db.PostReactions.Remove(r);
        await db.SaveChangesAsync(ct);
    }

    public async Task ReportPostAsync(Guid userId, Guid postId, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw Invalid("reason", "اكتب سبب البلاغ.");
        var reportedPost = await db.Posts.FirstOrDefaultAsync(x => x.PostId == postId, ct) ?? throw AppException.NotFound();
        await RequireVisibleCommunityAsync(userId, reportedPost.CommunityId, ct);
        db.ContentReports.Add(new ContentReport { ReportId = Guid.NewGuid(), ReporterId = userId, PostId = postId, Reason = reason.Trim(), Status = "open" });
        await db.SaveChangesAsync(ct);
    }

    public async Task ReportCommentAsync(Guid userId, Guid commentId, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw Invalid("reason", "اكتب سبب البلاغ.");
        if (!await db.Comments.AnyAsync(x => x.CommentId == commentId, ct)) throw AppException.NotFound();
        db.ContentReports.Add(new ContentReport { ReportId = Guid.NewGuid(), ReporterId = userId, CommentId = commentId, Reason = reason.Trim(), Status = "open" });
        await db.SaveChangesAsync(ct);
    }

    public async Task FollowAsync(Guid userId, Guid targetId, CancellationToken ct = default)
    {
        if (userId == targetId) throw Invalid("userId", "ما ينفعش تتابع نفسك.");
        if (!await db.UserAccounts.AnyAsync(u => u.UserId == targetId, ct)) throw AppException.NotFound();
        if (await db.UserFollows.AnyAsync(f => f.FollowerId == userId && f.FollowedId == targetId, ct)) return;
        db.UserFollows.Add(new UserFollow { FollowerId = userId, FollowedId = targetId, CreatedAt = clock.UtcNow });
        await db.SaveChangesAsync(ct);
    }

    public async Task UnfollowAsync(Guid userId, Guid targetId, CancellationToken ct = default)
    {
        var f = await db.UserFollows.FirstOrDefaultAsync(x => x.FollowerId == userId && x.FollowedId == targetId, ct);
        if (f is null) return;
        db.UserFollows.Remove(f);
        await db.SaveChangesAsync(ct);
    }

    public async Task<PublicProfileDto> GetProfileAsync(Guid viewerId, Guid userId, CancellationToken ct = default)
    {
        var u = await db.UserAccounts.Where(x => x.UserId == userId)
            .Select(x => new { x.UserId, x.CreatedAt, Role = x.Role.RoleName })
            .FirstOrDefaultAsync(ct) ?? throw AppException.NotFound();
        var profile = await db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId, ct);
        var isMe = viewerId == userId;
        var isFollower = !isMe && await db.UserFollows.AnyAsync(f => f.FollowerId == viewerId && f.FollowedId == userId, ct);
        // A non-public profile shows the identity fields only; bio, city, avatar and the
        // role-specific blocks stay between the owner and the audience he chose.
        var visibility = profile?.Visibility ?? "public";
        var visible = isMe || visibility == "public" || (visibility == "followers" && isFollower)
            || visibility is not ("public" or "followers" or "private");

        VolunteerProfileDto? volunteer = null;
        OrganizationProfileDto? organization = null;
        string? needType = null;
        if (visible && u.Role == "volunteer")
        {
            var vp = await db.VolunteerProfiles.FirstOrDefaultAsync(v => v.UserId == userId, ct);
            var skills = await db.UserAccounts.Where(a => a.UserId == userId).SelectMany(a => a.Skills).Select(s => s.Name).ToListAsync(ct);
            var interests = await db.UserAccounts.Where(a => a.UserId == userId).SelectMany(a => a.Interests).Select(i => i.Name).ToListAsync(ct);
            if (vp is not null || skills.Count > 0 || interests.Count > 0)
            {
                volunteer = new VolunteerProfileDto(vp?.Introduction, vp?.Governorate, vp?.ServiceArea ?? "", vp?.WeeklyHours ?? 0,
                    vp?.SkillLevel ?? "", vp?.VerificationStatus ?? "", skills, interests);
            }
        }
        else if (visible && u.Role is "association" or "company" or "shop")
        {
            var org = await db.Organizations.FirstOrDefaultAsync(o => o.OwnerUserId == userId, ct);
            if (org is not null)
            {
                organization = new OrganizationProfileDto(org.OrganizationId, org.Name, org.OrganizationType, org.Field, org.Description,
                    org.Governorate, org.City, org.Website,
                    string.IsNullOrWhiteSpace(org.LogoStorageKey) ? null : storage.GetPublicUrl(org.LogoStorageKey),
                    org.ApprovalStatus);
            }
        }
        else if (visible && u.Role == "disabled_user")
        {
            needType = await db.AccessibilityProfiles.Where(a => a.UserId == userId).Select(a => a.NeedType.Name).FirstOrDefaultAsync(ct);
        }

        return new PublicProfileDto(userId, profile?.DisplayName ?? "مستخدم", u.Role,
            await db.UserFollows.CountAsync(f => f.FollowedId == userId, ct), await db.UserFollows.CountAsync(f => f.FollowerId == userId, ct),
            await db.Posts.CountAsync(p => p.AuthorId == userId && p.ModerationStatus == Published, ct),
            isFollower, isMe,
            visible ? profile?.Bio : null, visible ? profile?.City : null,
            visible && !string.IsNullOrWhiteSpace(profile?.AvatarStorageKey) ? storage.GetPublicUrl(profile.AvatarStorageKey) : null,
            u.CreatedAt, volunteer, organization, needType);
    }

    public async Task<IReadOnlyList<ContentReportDto>> GetReportsAsync(string? status, CancellationToken ct = default)
    {
        IQueryable<ContentReport> q = db.ContentReports;
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(r => r.Status == status);
        var rows = await q.OrderBy(r => r.Status).ThenByDescending(r => r.ReportId).Take(100).ToListAsync(ct);
        var postIds = rows.Where(r => r.PostId != null).Select(r => r.PostId!.Value).ToList();
        var commentIds = rows.Where(r => r.CommentId != null).Select(r => r.CommentId!.Value).ToList();
        var posts = await db.Posts.Where(p => postIds.Contains(p.PostId)).ToDictionaryAsync(p => p.PostId, p => p.Body, ct);
        var comments = await db.Comments.Where(c => commentIds.Contains(c.CommentId)).ToDictionaryAsync(c => c.CommentId, c => c.Body, ct);
        var names = await NamesAsync(rows.Select(r => r.ReporterId), ct);
        return rows.Select(r => new ContentReportDto(r.ReportId, r.PostId, r.CommentId,
            r.PostId != null ? (posts.TryGetValue(r.PostId.Value, out var pb) ? pb : "") : (comments.TryGetValue(r.CommentId!.Value, out var cb) ? cb : ""),
            names.GetValueOrDefault(r.ReporterId, "مستخدم"), r.Reason, r.Status, r.Resolution)).ToList();
    }

    public async Task ResolveReportAsync(Guid adminId, Guid reportId, bool hide, string? resolution, CancellationToken ct = default)
    {
        var r = await db.ContentReports.FirstOrDefaultAsync(x => x.ReportId == reportId, ct) ?? throw AppException.NotFound();
        if (r.Status != "open") throw AppException.Conflict(message: "البلاغ اتقفل قبل كده.");
        if (hide)
        {
            if (r.PostId is { } pid) { var p = await db.Posts.FirstOrDefaultAsync(x => x.PostId == pid, ct); if (p != null) p.ModerationStatus = Hidden; }
            if (r.CommentId is { } cid) { var c = await db.Comments.FirstOrDefaultAsync(x => x.CommentId == cid, ct); if (c != null) c.ModerationStatus = Hidden; }
        }
        r.Status = hide ? "actioned" : "dismissed";
        r.Resolution = string.IsNullOrWhiteSpace(resolution) ? null : resolution.Trim();
        r.ReviewerId = adminId;
        await db.SaveChangesAsync(ct);
    }

    private async Task<List<PostDto>> ToDtosAsync(List<Post> rows, Guid viewer, CancellationToken ct)
    {
        var ids = rows.Select(p => p.PostId).ToList();
        var names = await NamesAsync(rows.Select(p => p.AuthorId), ct);
        var aids = rows.Select(p => p.AuthorId).Distinct().ToList();
        var roles = await (from u in db.UserAccounts join r in db.AccountRoles on u.RoleId equals r.RoleId where aids.Contains(u.UserId) select new { u.UserId, r.RoleName }).ToDictionaryAsync(x => x.UserId, x => x.RoleName, ct);
        var cids = rows.Select(p => p.CommunityId).Distinct().ToList();
        var cnames = await db.Communities.Where(c => cids.Contains(c.CommunityId)).ToDictionaryAsync(c => c.CommunityId, c => c.Name, ct);
        var rc = await db.PostReactions.Where(r => ids.Contains(r.PostId)).GroupBy(r => r.PostId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N, ct);
        var cc = await db.Comments.Where(c => ids.Contains(c.PostId) && c.ModerationStatus == Published).GroupBy(c => c.PostId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N, ct);
        var mine = await db.PostReactions.Where(r => ids.Contains(r.PostId) && r.UserId == viewer).ToDictionaryAsync(r => r.PostId, r => r.ReactionType, ct);
        var media = await db.PostMedia.Where(m => ids.Contains(m.PostId)).ToListAsync(ct);
        var mediaByPost = media.GroupBy(m => m.PostId).ToDictionary(g => g.Key,
            g => (IReadOnlyList<PostMediaDto>)g.Select(m => new PostMediaDto(m.MediaId, storage.GetPublicUrl(m.StorageKey), m.AlternativeText)).ToList());
        return rows.Select(p => new PostDto(p.PostId, p.AuthorId, names.GetValueOrDefault(p.AuthorId, "مستخدم"), roles.GetValueOrDefault(p.AuthorId, ""), p.CommunityId,
            cnames.GetValueOrDefault(p.CommunityId, ""), p.Body, p.CreatedAt, rc.GetValueOrDefault(p.PostId), cc.GetValueOrDefault(p.PostId), mine.GetValueOrDefault(p.PostId),
            mediaByPost.GetValueOrDefault(p.PostId, []))).ToList();
    }
}
