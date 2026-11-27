namespace HamzetWasl.Application.Contracts.Social;

public sealed record CreateCommunityRequest(string Name, string Description, string Rules);
public sealed record CreatePostRequest(Guid CommunityId, string Body);
public sealed record CreateCommentRequest(string Body);
public sealed record ReactRequest(string Type);
public sealed record ContentReportRequest(string Reason);
public sealed record ResolveReportRequest(bool Hide, string? Resolution);

public sealed record CommunityDto(Guid CommunityId, string Name, string Description, string Rules, int MembersCount, int PostsCount, bool Joined);

public sealed record PostDto(
    Guid PostId, Guid AuthorId, string AuthorName, string AuthorRole, Guid CommunityId, string CommunityName,
    string Body, DateTime CreatedAt, int ReactionsCount, int CommentsCount, string? MyReaction);

public sealed record CommentDto(Guid CommentId, Guid AuthorId, string AuthorName, string Body, DateTime CreatedAt);

public sealed record PostDetailDto(PostDto Post, IReadOnlyList<CommentDto> Comments);

public sealed record PublicProfileDto(
    Guid UserId, string DisplayName, string Role, int FollowersCount, int FollowingCount, int PostsCount, bool IsFollowing, bool IsMe);

public sealed record ContentReportDto(
    Guid ReportId, Guid? PostId, Guid? CommentId, string TargetText, string ReporterName, string Reason, string Status, string? Resolution);
