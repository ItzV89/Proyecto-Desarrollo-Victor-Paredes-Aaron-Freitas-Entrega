using Microsoft.EntityFrameworkCore;
using AuthUser.Api.Domain.Entities;

namespace AuthUser.Api.Infrastructure.Persistence;

public class AuthUserDbContext : DbContext
{
 public AuthUserDbContext(DbContextOptions<AuthUserDbContext> options) : base(options) { }

 public DbSet<Profile> Profiles { get; set; } = null!;

 protected override void OnModelCreating(ModelBuilder modelBuilder)
 {
 base.OnModelCreating(modelBuilder);
 modelBuilder.Entity<Profile>(b =>
 {
 b.HasKey(x => x.Id);
                                b.ToTable("profiles");
                                b.Property(x => x.Id).HasColumnName("id");
                                b.Property(x => x.Username).HasColumnName("username").IsRequired();
                                b.Property(x => x.Email).HasColumnName("email").IsRequired();
                                b.Property(x => x.KeycloakId).HasColumnName("keycloakid").IsRequired(false);
                                b.Property(x => x.Roles).HasColumnName("roles").IsRequired(false);
                                b.Property(x => x.CreatedAt).HasColumnName("createdat");
 });
 }
}
