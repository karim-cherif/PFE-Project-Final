using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PFE_Back.models;


namespace PFE_Back.data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Document> Documents { get; set; }
        public DbSet<TextCorrection> TextCorrections { get; set; }
        public DbSet<CorrectionAlternative> CorrectionAlternatives { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<ActivityLog> ActivityLogs { get; set; }
        public DbSet<ShareToken> ShareTokens { get; set; }
        public DbSet<OcrStatistics> OcrStatistics { get; set; }
        public DbSet<DeletionStats> DeletionStats { get; set; }
        public DbSet<DocumentProcessingHistory> DocumentProcessingHistory { get; set; }
        public DbSet<UserSettings> UserSettings { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<RefreshToken>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.UserId);
                entity.Property(e => e.Token).IsRequired();
                entity.Property(e => e.ExpiryDate).IsRequired();
                entity.HasOne(e => e.User)
                      .WithMany(u => u.RefreshTokens)
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<Document>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.UploadDate);
                entity.HasIndex(e => e.DeletedAt);
                entity.Property(e => e.OcrResponse).HasColumnType("nvarchar(max)");
                entity.Property(e => e.DeletedAt).IsRequired(false);
                entity.HasOne(e => e.User)
                      .WithMany(u => u.Documents)
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<TextCorrection>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.DocumentId);
                entity.HasOne(e => e.Document)
                      .WithMany(d => d.Corrections)
                      .HasForeignKey(e => e.DocumentId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<CorrectionAlternative>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.CorrectionId);
                entity.HasOne(e => e.Correction)
                      .WithMany(c => c.Alternatives)
                      .HasForeignKey(e => e.CorrectionId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<ActivityLog>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.UserId);
                entity.Property(e => e.Action).IsRequired();
                entity.Property(e => e.Timestamp).IsRequired();
                // Add foreign key relationship with cascade delete
                entity.HasOne<ApplicationUser>()
                      .WithMany()
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<ShareToken>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.DocumentId);
                entity.HasIndex(e => e.Token).IsUnique();
                entity.HasOne(e => e.Document)
                      .WithMany(d => d.ShareTokens)
                      .HasForeignKey(e => e.DocumentId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<DocumentProcessingHistory>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.DocumentId);
                entity.HasIndex(e => e.UserId);
                entity.Property(e => e.OcrResponse).IsRequired();
                entity.HasOne(e => e.Document)
                      .WithMany()
                      .HasForeignKey(e => e.DocumentId)
                      .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.User)
                      .WithMany()
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.NoAction); // Keep as NoAction to avoid cascade cycles
            });

            // Add missing entity configurations
            builder.Entity<OcrStatistics>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.UserId);
                entity.HasOne(e => e.User)
                      .WithMany()
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<DeletionStats>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.UserId);
                entity.HasOne(e => e.User)
                      .WithMany()
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<UserSettings>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.UserId).IsUnique();
                entity.Property(e => e.SettingsData).IsRequired();
                entity.HasOne(e => e.User)
                      .WithOne()
                      .HasForeignKey<UserSettings>(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            SeedAdminUser(builder);
        }

        private void SeedAdminUser(ModelBuilder builder)
        {


            var adminRoleId = Guid.Parse("00000000-0000-0000-0000-000000000001");
            var userRoleId = Guid.Parse("00000000-0000-0000-0000-000000000002");
            var adminUserId = Guid.Parse("00000000-0000-0000-0000-000000000003");

            builder.Entity<IdentityRole<Guid>>().HasData(
                new IdentityRole<Guid>
                {
                    Id = adminRoleId,
                    Name = "Admin",
                    NormalizedName = "ADMIN",
                    ConcurrencyStamp = "ADMIN_ROLE_CONCURRENCY_STAMP"
                },
                new IdentityRole<Guid>
                {
                    Id = userRoleId,
                    Name = "User",
                    NormalizedName = "USER",
                    ConcurrencyStamp = "USER_ROLE_CONCURRENCY_STAMP"
                }
            );

            var adminUser = new ApplicationUser
            {
                Id = adminUserId,
                UserName = "admin@example.com",
                NormalizedUserName = "ADMIN@EXAMPLE.COM",
                Email = "admin@example.com",
                NormalizedEmail = "ADMIN@EXAMPLE.COM",
                EmailConfirmed = true,
                FirstName = "Admin",
                LastName = "User",
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                CreatedBy = Guid.Empty,
                SecurityStamp = "ADMIN_USER_SECURITY_STAMP",
                ConcurrencyStamp = "ADMIN_USER_CONCURRENCY_STAMP",
                PasswordHash = "AQAAAAIAAYagAAAAEI+DDTGQuKY4ulmuW1ke78VJwqPOKfeLNfNHJFPB6o8fYzyKJtzKCX83gIeLADs08g==" // Precomputed hash for "AdminPass123!"
            };

            builder.Entity<ApplicationUser>().HasData(adminUser);

            builder.Entity<IdentityUserRole<Guid>>().HasData(
                new IdentityUserRole<Guid>
                {
                    RoleId = adminRoleId,
                    UserId = adminUserId
                }
            );
        }
    }
}

