namespace Miniblog.Core.Database
{
    using Microsoft.EntityFrameworkCore;

    using Miniblog.Core.Database.Models;

    public class BlogContext : DbContext
    {
        public DbSet<PostDb> Posts { get; set; }
        public DbSet<CategoryDb> Categories { get; set; }
        public DbSet<TagDb> Tags { get; set; }
        public DbSet<CommentDb> Comments { get; set; }
        public DbSet<FileDb> Files { get; set; }

        public BlogContext(DbContextOptions<BlogContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PostDb>(post =>
            {
                post.HasIndex(p => p.Slug).IsUnique();

                post.HasMany(p => p.Comments)
                    .WithOne(c => c.Post)
                    .OnDelete(DeleteBehavior.Cascade);

                post.HasMany(p => p.Categories)
                    .WithOne(c => c.Post)
                    .OnDelete(DeleteBehavior.Cascade);

                post.HasMany(p => p.Tags)
                    .WithOne(t => t.Post)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
