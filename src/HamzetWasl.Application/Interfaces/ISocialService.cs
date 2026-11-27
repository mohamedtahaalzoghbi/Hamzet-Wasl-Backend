using HamzetWasl.Application.Common;
using HamzetWasl.Application.Contracts.Social;

namespace HamzetWasl.Application.Interfaces;

public interface ISocialService
{
    Task<IReadOnlyList<CommunityDto>> GetCommunitiesAsync(Guid userId, CancellationToken ct = default);
    Task<CommunityDto> CreateCommunityAsync(Guid adminId, CreateCommunityRequest r, CancellationToken ct = default);
    Task JoinAsync(Guid userId, Guid communityId, CancellationToken ct = default);
    Task LeaveAsync(Guid userId, Guid communityId, CancellationToken ct = default);
    Task<PagedResult<PostDto>> GetFeedAsync(Guid userId, Guid? communityId, Guid? authorId, bool followingOnly, int page, int pageSize, CancellationToken ct = default);
    Task<PostDto> CreatePostAsync(Guid userId, CreatePostRequest r, CancellationToken ct = default);
    Task<PostMediaDto> AddPostMediaAsync(Guid userId, Guid postId, string altText, FileUpload file, CancellationToken ct = default);
    Task<PostDetailDto> GetPostAsync(Guid userId, Guid postId, CancellationToken ct = default);
    Task DeletePostAsync(Guid userId, bool isAdmin, Guid postId, CancellationToken ct = default);
    Task<CommentDto> AddCommentAsync(Guid userId, Guid postId, string body, CancellationToken ct = default);
    Task ReactAsync(Guid userId, Guid postId, string type, CancellationToken ct = default);
    Task UnreactAsync(Guid userId, Guid postId, CancellationToken ct = default);
    Task ReportPostAsync(Guid userId, Guid postId, string reason, CancellationToken ct = default);
    Task ReportCommentAsync(Guid userId, Guid commentId, string reason, CancellationToken ct = default);
    Task FollowAsync(Guid userId, Guid targetId, CancellationToken ct = default);
    Task UnfollowAsync(Guid userId, Guid targetId, CancellationToken ct = default);
    Task<PublicProfileDto> GetProfileAsync(Guid viewerId, Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<ContentReportDto>> GetReportsAsync(string? status, CancellationToken ct = default);
    Task ResolveReportAsync(Guid adminId, Guid reportId, bool hide, string? resolution, CancellationToken ct = default);
}
