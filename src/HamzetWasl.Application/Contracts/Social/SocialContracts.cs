namespace HamzetWasl.Application.Contracts.Social;

public sealed record CreateCommunityRequest(string Name, string Description, string Rules);
public sealed record CreatePostRequest(Guid CommunityId, string Body);
public sealed record CreateCommentRequest(string Body);
public sealed record ReactRequest(string Type);
public sealed record ContentReportRequest(string Reason);
public sealed record ResolveReportRequest(bool Hide, string? Resolution);

public sealed record CommunityDto(Guid CommunityId, string Name, string Description, string Rules, int MembersCount, int PostsCount, bool Joined);

public sealed record PostMediaDto(Guid MediaId, string Url, string? AltText);

public sealed record PostDto(
    Guid PostId, Guid AuthorId, string AuthorName, string AuthorRole, Guid CommunityId, string CommunityName,
    string Body, DateTime CreatedAt, int ReactionsCount, int CommentsCount, string? MyReaction,
    IReadOnlyList<PostMediaDto> Media);

public sealed record CommentDto(Guid CommentId, Guid AuthorId, string AuthorName, string Body, DateTime CreatedAt);

public sealed record PostDetailDto(PostDto Post, IReadOnlyList<CommentDto> Comments);

/// <summary>Volunteer block of the public profile: how the volunteer presents himself and where he helps.</summary>
public sealed record VolunteerProfileDto(
    string? Introduction, string? Governorate, string ServiceArea, int WeeklyHours,
    string SkillLevel, string VerificationStatus, IReadOnlyList<string> Skills, IReadOnlyList<string> Interests);

/// <summary>Organization block of the public profile (association, company or shop).</summary>
public sealed record OrganizationProfileDto(
    Guid OrganizationId, string Name, string OrganizationType, string? Field, string? Description,
    string? Governorate, string? City, string? Website, string? LogoUrl, string ApprovalStatus);

/// <summary>
/// The full public profile of any account. Bio, city, avatar and the role-specific block
/// (Volunteer / Organization / NeedType) are hidden when the profile is private and the
/// viewer is someone else.
/// </summary>
public sealed record PublicProfileDto(
    Guid UserId, string DisplayName, string Role, int FollowersCount, int FollowingCount, int PostsCount, bool IsFollowing, bool IsMe,
    string? Bio, string? City, string? AvatarUrl, DateTime MemberSince,
    VolunteerProfileDto? Volunteer, OrganizationProfileDto? Organization, string? NeedType);

public sealed record ContentReportDto(
    Guid ReportId, Guid? PostId, Guid? CommentId, string TargetText, string ReporterName, string Reason, string Status, string? Resolution);
