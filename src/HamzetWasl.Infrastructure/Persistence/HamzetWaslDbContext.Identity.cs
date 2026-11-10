using HamzetWasl.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;

namespace HamzetWasl.Infrastructure.Persistence;

public partial class HamzetWaslDbContext
{
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>(entity =>
        {
            // The two rows are created in the same transaction. The FK points from
            // the Identity-only table to the authoritative USER_ACCOUNT table.
            entity.HasOne(e => e.UserAccount)
                .WithOne()
                .HasForeignKey<AppUser>(e => e.Id)
                .HasPrincipalKey<Domain.Entities.UserAccount>(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_AspNetUsers_USER_ACCOUNT");
        });
    }
}
