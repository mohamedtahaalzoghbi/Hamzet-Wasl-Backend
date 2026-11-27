using HamzetWasl.Application.Common;
using HamzetWasl.Application.Contracts.Social;
using HamzetWasl.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HamzetWasl.Api.Controllers;

[Authorize]
[Route("api/v1/social")]
public sealed class SocialController(ISocialService social) : ApiControllerBase
{
    private bool IsAdmin => CurrentRole == "admin";

    /// <summary>[Bearer Token] Lists the communities with member counts and whether the caller joined.</summary>
    [HttpGet("communities")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CommunityDto>>>> Communities(CancellationToken ct) =>
        Ok(Envelope(await social.GetCommunitiesAsync(CurrentUserId, ct)));

    /// <summary>[Bearer Token — Admin] Creates a community.</summary>
    [HttpPost("communities")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult<ApiResponse<CommunityDto>>> CreateCommunity([FromBody] CreateCommunityRequest request, CancellationToken ct) =>
        CreatedEnvelope(await social.CreateCommunityAsync(CurrentUserId, request, ct));

    /// <summary>[Bearer Token] Joins a community.</summary>
    [HttpPost("communities/{id:guid}/join")]
    public async Task<IActionResult> Join(Guid id, CancellationToken ct) { await social.JoinAsync(CurrentUserId, id, ct); return NoContent(); }

    /// <summary>[Bearer Token] Leaves a community.</summary>
    [HttpDelete("communities/{id:guid}/join")]
    public async Task<IActionResult> Leave(Guid id, CancellationToken ct) { await social.LeaveAsync(CurrentUserId, id, ct); return NoContent(); }

    /// <summary>[Bearer Token] The timeline: published posts, newest first. Filters: communityId, authorId, following=true.</summary>
    [HttpGet("posts")]
    public async Task<ActionResult<ApiResponse<PagedResult<PostDto>>>> Feed(
        [FromQuery] Guid? communityId, [FromQuery] Guid? authorId, [FromQuery] bool following = false,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(Envelope(await social.GetFeedAsync(CurrentUserId, communityId, authorId, following, page, pageSize, ct)));

    /// <summary>[Bearer Token — not admin] Publishes a post in a community the caller joined.</summary>
    [HttpPost("posts")]
    [Authorize(Policy = "NonAdmin")]
    public async Task<ActionResult<ApiResponse<PostDto>>> CreatePost([FromBody] CreatePostRequest request, CancellationToken ct) =>
        CreatedEnvelope(await social.CreatePostAsync(CurrentUserId, request, ct));

    /// <summary>[Bearer Token] A post with its comments.</summary>
    [HttpGet("posts/{id:guid}")]
    public async Task<ActionResult<ApiResponse<PostDetailDto>>> GetPost(Guid id, CancellationToken ct) =>
        Ok(Envelope(await social.GetPostAsync(CurrentUserId, id, ct)));

    /// <summary>[Bearer Token] Hides a post (the author, or an admin).</summary>
    [HttpDelete("posts/{id:guid}")]
    public async Task<IActionResult> DeletePost(Guid id, CancellationToken ct) { await social.DeletePostAsync(CurrentUserId, IsAdmin, id, ct); return NoContent(); }

    /// <summary>[Bearer Token — not admin] Adds a comment.</summary>
    [HttpPost("posts/{id:guid}/comments")]
    [Authorize(Policy = "NonAdmin")]
    public async Task<ActionResult<ApiResponse<CommentDto>>> Comment(Guid id, [FromBody] CreateCommentRequest request, CancellationToken ct) =>
        CreatedEnvelope(await social.AddCommentAsync(CurrentUserId, id, request.Body, ct));

    /// <summary>[Bearer Token] Sets the caller's reaction (like|support|thanks).</summary>
    [HttpPut("posts/{id:guid}/reaction")]
    public async Task<IActionResult> React(Guid id, [FromBody] ReactRequest request, CancellationToken ct) { await social.ReactAsync(CurrentUserId, id, request.Type, ct); return NoContent(); }

    /// <summary>[Bearer Token] Removes the caller's reaction.</summary>
    [HttpDelete("posts/{id:guid}/reaction")]
    public async Task<IActionResult> Unreact(Guid id, CancellationToken ct) { await social.UnreactAsync(CurrentUserId, id, ct); return NoContent(); }

    /// <summary>[Bearer Token] Reports a post.</summary>
    [HttpPost("posts/{id:guid}/report")]
    public async Task<IActionResult> ReportPost(Guid id, [FromBody] ContentReportRequest request, CancellationToken ct) { await social.ReportPostAsync(CurrentUserId, id, request.Reason, ct); return NoContent(); }

    /// <summary>[Bearer Token] Reports a comment.</summary>
    [HttpPost("comments/{id:guid}/report")]
    public async Task<IActionResult> ReportComment(Guid id, [FromBody] ContentReportRequest request, CancellationToken ct) { await social.ReportCommentAsync(CurrentUserId, id, request.Reason, ct); return NoContent(); }

    /// <summary>[Bearer Token] Public profile summary of a user.</summary>
    [HttpGet("users/{id:guid}")]
    public async Task<ActionResult<ApiResponse<PublicProfileDto>>> Profile(Guid id, CancellationToken ct) =>
        Ok(Envelope(await social.GetProfileAsync(CurrentUserId, id, ct)));

    /// <summary>[Bearer Token] Follows a user.</summary>
    [HttpPut("users/{id:guid}/follow")]
    public async Task<IActionResult> Follow(Guid id, CancellationToken ct) { await social.FollowAsync(CurrentUserId, id, ct); return NoContent(); }

    /// <summary>[Bearer Token] Unfollows a user.</summary>
    [HttpDelete("users/{id:guid}/follow")]
    public async Task<IActionResult> Unfollow(Guid id, CancellationToken ct) { await social.UnfollowAsync(CurrentUserId, id, ct); return NoContent(); }

    /// <summary>[Bearer Token — Admin] Content reports (status open|actioned|dismissed).</summary>
    [HttpGet("reports")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ContentReportDto>>>> Reports([FromQuery] string? status, CancellationToken ct) =>
        Ok(Envelope(await social.GetReportsAsync(status, ct)));

    /// <summary>[Bearer Token — Admin] Resolves a report: hide=true hides the content.</summary>
    [HttpPost("reports/{id:guid}/resolve")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> Resolve(Guid id, [FromBody] ResolveReportRequest request, CancellationToken ct) { await social.ResolveReportAsync(CurrentUserId, id, request.Hide, request.Resolution, ct); return NoContent(); }
}
