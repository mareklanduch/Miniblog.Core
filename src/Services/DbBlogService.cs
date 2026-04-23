namespace Miniblog.Core.Services
{
    using Microsoft.AspNetCore.Http;
    using Microsoft.EntityFrameworkCore;

    using Miniblog.Core.Database;
    using Miniblog.Core.Database.Models;
    using Miniblog.Core.Models;

    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;

    public class DbBlogService : IBlogService
    {
        private readonly IHttpContextAccessor _contextAccessor;
        private readonly BlogContext _blogContext;

        public DbBlogService(IHttpContextAccessor contextAccessor, BlogContext blogContext)
        {
            _contextAccessor = contextAccessor;
            _blogContext = blogContext;
        }

        public async Task DeletePost(Post post)
        {
            ArgumentNullException.ThrowIfNull(post);

            _ = Guid.TryParse(post.ID, out var postId);
            var entity = await _blogContext.Posts.FindAsync(postId);
            if (entity is not null)
            {
                _blogContext.Remove(entity);
                await _blogContext.SaveChangesAsync();
            }
        }

        public IAsyncEnumerable<string> GetCategories()
        {
            var isAdmin = IsAdmin();
            return _blogContext.Categories
                .Where(c => c.Post.IsPublished || isAdmin)
                .Select(c => c.Name!.ToLower())
                .Distinct()
                .AsAsyncEnumerable();
        }

        public IAsyncEnumerable<string> GetTags()
        {
            var isAdmin = IsAdmin();
            return _blogContext.Tags
                .Where(t => t.Post.IsPublished || isAdmin)
                .Select(t => t.Name!.ToLower())
                .Distinct()
                .AsAsyncEnumerable();
        }

        public async Task<Post?> GetPostById(string id)
        {
            _ = Guid.TryParse(id, out var postId);
            var post = await PostsWithIncludes()
                .AsNoTracking()
                .FirstOrDefaultAsync(p =>
                    p.ID == postId
                    && ((p.PubDate < DateTime.UtcNow && p.IsPublished) || IsAdmin()));
            return MapEntityToPost(post);
        }

        public async Task<Post?> GetPostBySlug(string slug)
        {
            var decodedSlug = System.Net.WebUtility.UrlDecode(slug).ToLower();
            var post = await PostsWithIncludes()
                .AsNoTracking()
                .FirstOrDefaultAsync(p =>
                    p.Slug!.ToLower() == decodedSlug
                    && ((p.PubDate < DateTime.UtcNow && p.IsPublished) || IsAdmin()));
            return MapEntityToPost(post);
        }

        public IAsyncEnumerable<Post> GetPosts()
        {
            return VisiblePosts(IsAdmin())
                .OrderByDescending(p => p.PubDate)
                .AsAsyncEnumerable()
                .Select(p => MapEntityToPost(p)!);
        }

        public IAsyncEnumerable<Post> GetPosts(int count, int skip = 0)
        {
            return VisiblePosts(IsAdmin())
                .OrderByDescending(p => p.PubDate)
                .Skip(skip)
                .Take(count)
                .AsAsyncEnumerable()
                .Select(p => MapEntityToPost(p)!);
        }

        public IAsyncEnumerable<Post> GetPostsByCategory(string category)
        {
            return VisiblePosts(IsAdmin())
                .Where(p => p.Categories.Any(c => c.Name!.ToLower() == category.ToLower()))
                .OrderByDescending(p => p.PubDate)
                .AsAsyncEnumerable()
                .Select(p => MapEntityToPost(p)!);
        }

        public IAsyncEnumerable<Post> GetPostsByTag(string tag)
        {
            return VisiblePosts(IsAdmin())
                .Where(p => p.Tags.Any(t => t.Name!.ToLower() == tag.ToLower()))
                .OrderByDescending(p => p.PubDate)
                .AsAsyncEnumerable()
                .Select(p => MapEntityToPost(p)!);
        }

        public async Task<string> SaveFile(byte[] bytes, string fileName, string? suffix = null)
        {
            ArgumentNullException.ThrowIfNull(bytes);

            suffix ??= DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture);
            var ext = Path.GetExtension(fileName);
            var name = Path.GetFileNameWithoutExtension(fileName);
            var fileNameWithSuffix = $"{name}_{suffix}{ext}";

            _blogContext.Files.Add(new FileDb { FileName = fileNameWithSuffix, Content = bytes });
            await _blogContext.SaveChangesAsync();

            return $"/file/{fileNameWithSuffix}";
        }

        public async Task SavePost(Post post)
        {
            ArgumentNullException.ThrowIfNull(post);

            _ = Guid.TryParse(post.ID, out var postId);
            var entity = await PostsWithIncludes()
                .FirstOrDefaultAsync(p => p.ID == postId) ?? new PostDb();

            post.LastModified = DateTime.UtcNow;
            BindPostToEntity(post, entity);

            if (entity.ID == Guid.Empty)
                _ = await _blogContext.Posts.AddAsync(entity);
            else
                _blogContext.Posts.Update(entity);

            await _blogContext.SaveChangesAsync();
        }

        private bool IsAdmin() => _contextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true;

        private IQueryable<PostDb> PostsWithIncludes() =>
            _blogContext.Posts
                .Include(p => p.Comments)
                .Include(p => p.Categories)
                .Include(p => p.Tags);

        private IQueryable<PostDb> VisiblePosts(bool isAdmin) =>
            PostsWithIncludes()
                .AsNoTracking()
                .Where(p => (p.PubDate < DateTime.UtcNow && p.IsPublished) || isAdmin);

        private static void BindPostToEntity(Post post, PostDb entity)
        {
            entity.Content = post.Content;
            entity.Excerpt = post.Excerpt;
            entity.IsPublished = post.IsPublished;
            entity.LastModified = post.LastModified;
            entity.PubDate = post.PubDate;
            entity.Slug = post.Slug;
            entity.Title = post.Title;

            var commentsPosted = post.Comments.Select(c =>
            {
                _ = Guid.TryParse(c.ID, out var commentId);
                return new CommentDb
                {
                    ID = commentId,
                    Author = c.Author,
                    Email = c.Email,
                    Content = c.Content,
                    IsAdmin = c.IsAdmin,
                    PubDate = c.PubDate
                };
            }).ToList();

            var newComments = commentsPosted
                .ExceptBy(entity.Comments.Select(c => c.ID), c => c.ID)
                .ToList();
            newComments.ForEach(c => c.ID = Guid.Empty);
            newComments.AddRange(entity.Comments.IntersectBy(
                post.Comments.Select(c => c.ID.ToLower()),
                c => c.ID.ToString().ToLower()));
            entity.Comments = newComments;

            var newTags = post.Tags
                .Select(t => new TagDb { Name = t })
                .ExceptBy(entity.Tags.Select(t => t.Name), t => t.Name)
                .ToList();
            newTags.AddRange(entity.Tags.IntersectBy(post.Tags, t => t.Name));
            entity.Tags = newTags;

            var newCategories = post.Categories
                .Select(c => new CategoryDb { Name = c })
                .ExceptBy(entity.Categories.Select(c => c.Name), c => c.Name)
                .ToList();
            newCategories.AddRange(entity.Categories.IntersectBy(post.Categories, c => c.Name));
            entity.Categories = newCategories;
        }

        private static Post? MapEntityToPost(PostDb? post)
        {
            if (post is null) return null;

            var dto = new Post
            {
                ID = post.ID.ToString(),
                Content = post.Content ?? string.Empty,
                Excerpt = post.Excerpt ?? string.Empty,
                IsPublished = post.IsPublished,
                LastModified = post.LastModified,
                PubDate = post.PubDate,
                Slug = post.Slug ?? string.Empty,
                Title = post.Title ?? string.Empty
            };

            foreach (var comment in post.Comments)
            {
                dto.Comments.Add(new Comment
                {
                    ID = comment.ID.ToString(),
                    Author = comment.Author ?? string.Empty,
                    Content = comment.Content ?? string.Empty,
                    Email = comment.Email ?? string.Empty,
                    IsAdmin = comment.IsAdmin,
                    PubDate = comment.PubDate
                });
            }

            foreach (var tag in post.Tags)
                dto.Tags.Add(tag.Name!);

            foreach (var cat in post.Categories)
                dto.Categories.Add(cat.Name!);

            return dto;
        }
    }
}
