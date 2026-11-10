using HamzetWasl.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace HamzetWasl.Infrastructure.Identity;

/// <summary>
/// ASP.NET Core Identity credential record. The domain profile lives in
/// <see cref="UserAccount"/>; both rows share the same <see cref="IdentityUser{TKey}.Id"/>.
/// </summary>
public sealed class AppUser : IdentityUser<Guid>
{
    public UserAccount UserAccount { get; set; } = null!;
}
