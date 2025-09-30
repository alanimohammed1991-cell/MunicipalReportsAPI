using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MunicipalReportsAPI.Models;

namespace MunicipalReportsAPI.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<Report> Reports { get; set; }
        public DbSet<Category> Categories { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure Report -> Category relationship
            modelBuilder.Entity<Report>()
                .HasOne(r => r.Category)
                .WithMany()
                .HasForeignKey(r => r.CategoryId)
                .OnDelete(DeleteBehavior.Restrict); // Prevent cascade delete

            // Configure Report -> User relationship (report creator)
            modelBuilder.Entity<Report>()
                .HasOne(r => r.User)
                .WithMany(u => u.Reports)
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.SetNull); // Set to null when user is deleted



            // Add indexes for performance
            modelBuilder.Entity<Report>()
                .HasIndex(r => r.Status);

            modelBuilder.Entity<Report>()
                .HasIndex(r => r.CreatedAt);

            modelBuilder.Entity<Report>()
                .HasIndex(r => r.UserId);

            // Seed data
            SeedData(modelBuilder);
        }

        private static void SeedData(ModelBuilder modelBuilder)
        {
            // Seed Categories
            modelBuilder.Entity<Category>().HasData(
                new Category { Id = 1, Name = "Pothole", Icon = "road", Color = "#FF6B6B" },
                new Category { Id = 2, Name = "Street Light", Icon = "lightbulb", Color = "#4ECDC4" },
                new Category { Id = 3, Name = "Graffiti", Icon = "spray-can", Color = "#45B7D1" },
                new Category { Id = 4, Name = "Trash", Icon = "trash", Color = "#96CEB4" },
                new Category { Id = 5, Name = "Traffic Sign", Icon = "traffic-cone", Color = "#F39C12" },
                new Category { Id = 6, Name = "Water/Sewer", Icon = "droplet", Color = "#3498DB" },
                new Category { Id = 7, Name = "Parks/Recreation", Icon = "tree", Color = "#27AE60" },
                new Category { Id = 8, Name = "Other", Icon = "alert-circle", Color = "#FECA57" }
            );

            // Seed Roles
            modelBuilder.Entity<IdentityRole>().HasData(
                new IdentityRole
                {
                    Id = "1",
                    Name = "Admin",
                    NormalizedName = "ADMIN"
                },
                new IdentityRole
                {
                    Id = "2",
                    Name = "MunicipalStaff",
                    NormalizedName = "MUNICIPALSTAFF"
                },
                new IdentityRole
                {
                    Id = "3",
                    Name = "Citizen",
                    NormalizedName = "CITIZEN"
                }
            );
        }
    }
}