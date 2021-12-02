namespace Miniblog.Core.Services
{
    using Microsoft.AspNetCore.Http;
    using Microsoft.EntityFrameworkCore;

    using Miniblog.Core.Database;
    using Miniblog.Core.Database.Models;
    using Miniblog.Core.Models;

    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    public class DbBlogService : IBlogService
    {
        private readonly IHttpContextAccessor contextAccessor;
        private readonly BlogContext blogContext;

        public DbBlogService(
            IHttpContextAccessor contextAccessor,
            BlogContext blogContext)
        {
            this.contextAccessor = contextAccessor;
            this.blogContext = blogContext;
        }

        public async Task DeletePost(Post post)
        {
            if (post is null)
            {
                throw new ArgumentNullException(nameof(post));
            }

            _ = Guid.TryParse(post.ID, out var postId);

            var postEntity = await this.blogContext.Posts.FindAsync(postId);
            if (postEntity != null)
            {
                this.blogContext.Remove(postEntity);
                await this.blogContext.SaveChangesAsync();
            }
        }

        public IAsyncEnumerable<string> GetCategories()
        {
            var isAdmin = this.IsAdmin();

            return this.blogContext
                .Categories
                .Where(p => p.Post.IsPublished || isAdmin)
                .Select(cat => cat.Name.ToLowerInvariant())
                .Distinct()
                .ToAsyncEnumerable();
        }

        public IAsyncEnumerable<string> GetTags()
        {
            var isAdmin = this.IsAdmin();

            return this.blogContext
                .Tags
                .Where(p => p.Post.IsPublished || isAdmin)
                .Select(tag => tag.Name.ToLowerInvariant())
                .Distinct()
                .ToAsyncEnumerable();
        }

        public Task<Post?> GetPostById(string id)
        {
            var isAdmin = this.IsAdmin();

            _ = Guid.TryParse(id, out var postId);
            var post = this.blogContext
                .Posts
                .Include(nameof(PostDb.Comments))
                .Include(nameof(PostDb.Categories))
                .Include(nameof(PostDb.Tags))
                .FirstOrDefault(p =>
                p.ID == postId
                && ((p.PubDate < DateTime.UtcNow && p.IsPublished)
                    || isAdmin));

            return Task.FromResult(MapEntityToPost(post));
        }

        public Task<Post?> GetPostBySlug(string slug)
        {
            var isAdmin = this.IsAdmin();
            var decodedSlug = System.Net.WebUtility.UrlDecode(slug).ToLower();
            var post = this.blogContext
                .Posts
                .Include(nameof(PostDb.Comments))
                .Include(nameof(PostDb.Categories))
                .Include(nameof(PostDb.Tags))
                .FirstOrDefault(p =>
                p.Slug.ToLower().Equals(decodedSlug)
                && ((p.PubDate < DateTime.UtcNow && p.IsPublished)
                    || isAdmin));

            return Task.FromResult(MapEntityToPost(post));
        }

        public IAsyncEnumerable<Post> GetPosts()
        {
            var isAdmin = this.IsAdmin();

            return this.blogContext
                .Posts
                .Include(nameof(PostDb.Comments))
                .Include(nameof(PostDb.Categories))
                .Include(nameof(PostDb.Tags))
                .Where(p => (p.PubDate < DateTime.UtcNow && p.IsPublished)
                            || isAdmin)
                .Select(p => MapEntityToPost(p))
                .ToAsyncEnumerable()
                .OrderByDescending(p => p.PubDate);
        }

        public IAsyncEnumerable<Post> GetPosts(int count, int skip = 0)
        {
            var isAdmin = this.IsAdmin();

            return this.blogContext
                .Posts
                .Include(nameof(PostDb.Comments))
                .Include(nameof(PostDb.Categories))
                .Include(nameof(PostDb.Tags))
                .Where(p => (p.PubDate < DateTime.UtcNow && p.IsPublished)
                            || isAdmin)
                .Skip(skip)
                .Take(count)
                .Select(p => MapEntityToPost(p))
                .ToAsyncEnumerable();
        }

        public IAsyncEnumerable<Post> GetPostsByCategory(string category)
        {
            var isAdmin = this.IsAdmin();

            var posts = this.blogContext
                .Categories
                .Include(nameof(CategoryDb.Post))
                .Include($"{nameof(CategoryDb.Post)}.{nameof(PostDb.Comments)}")
                .Include($"{nameof(CategoryDb.Post)}.{nameof(PostDb.Categories)}")
                .Include($"{nameof(CategoryDb.Post)}.{nameof(PostDb.Tags)}")
                .Where(c =>
                c.Name.ToLower().Equals(category.ToLower())
                && ((c.Post.PubDate < DateTime.UtcNow && c.Post.IsPublished)
                    || isAdmin))
                .Select(c => MapEntityToPost(c.Post));


            return posts.ToAsyncEnumerable();
        }

        public IAsyncEnumerable<Post> GetPostsByTag(string tag)
        {
            var isAdmin = this.IsAdmin();

            var posts = this.blogContext
                .Tags
                .Include(nameof(TagDb.Post))
                .Include($"{nameof(CategoryDb.Post)}.{nameof(PostDb.Comments)}")
                .Include($"{nameof(CategoryDb.Post)}.{nameof(PostDb.Categories)}")
                .Include($"{nameof(CategoryDb.Post)}.{nameof(PostDb.Tags)}")
                .Where(t =>
                t.Name.ToLower().Equals(tag.ToLower())
                && ((t.Post.PubDate < DateTime.UtcNow && t.Post.IsPublished)
                    || isAdmin))
                .Select(t => MapEntityToPost(t.Post));


            return posts.ToAsyncEnumerable();
        }

        public Task<string> SaveFile(byte[] bytes, string fileName, string? suffix = null) => throw new NotImplementedException();

        public async Task SavePost(Post post)
        {
            if (post is null)
            {
                throw new ArgumentNullException(nameof(post));
            }

            _ = Guid.TryParse(post.ID, out var postId);


            var entity = this.blogContext
                .Posts
                .Include(nameof(PostDb.Comments))
                .Include(nameof(PostDb.Categories))
                .Include(nameof(PostDb.Tags))
                .FirstOrDefault(p => p.ID == postId) ?? new PostDb();

            post.LastModified = DateTime.UtcNow;

            BindPostToEntity(post, entity);

            if (entity.ID == Guid.Empty)
            {
                _ = await this.blogContext.Posts.AddAsync(entity);
            }
            else
            {
                this.blogContext.Posts.Update(entity);
            }
            await this.blogContext.SaveChangesAsync();
        }

        protected bool IsAdmin() => this.contextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true;

        private void BindPostToEntity(Post post, PostDb entity)
        {
            entity.Content = post.Content;
            entity.Excerpt = post.Excerpt;
            entity.IsPublished = post.IsPublished;
            entity.LastModified = post.LastModified;
            entity.PubDate = post.PubDate;
            entity.Slug = post.Slug;
            entity.Title = post.Title;

            entity.Comments = entity.Comments ?? new List<CommentDb>();
            entity.Tags = entity.Tags ?? new List<TagDb>();
            entity.Categories = entity.Categories ?? new List<CategoryDb>();

            var commentsPosted = post.Comments.Select(t =>
            {
                _ = Guid.TryParse(t.ID, out var commentId);
                return new CommentDb
                {
                    ID = commentId,
                    Author = t.Author,
                    Email = t.Email,
                    Content = t.Content,
                    IsAdmin = t.IsAdmin,
                    PubDate = t.PubDate
                };
            }).ToList();

            var newComments = commentsPosted.ExceptBy(entity.Comments.Select(t => t.ID), t => t.ID).ToList();
            newComments.ForEach(c => c.ID = Guid.Empty);
            newComments.AddRange(entity.Comments.IntersectBy(post.Comments.Select(c=>c.ID.ToLower()).ToList(), t => t.ID.ToString().ToLower()));
            entity.Comments = newComments;

            var tagsPosted = post.Tags.Select(t => new TagDb { Name = t }).ToList();
            var newTags = tagsPosted.ExceptBy(entity.Tags.Select(t => t.Name), t => t.Name).ToList();
            newTags.AddRange(entity.Tags.IntersectBy(post.Tags.ToList(), t => t.Name));
            entity.Tags = newTags;

            var catPosted = post.Categories.Select(c => new CategoryDb { Name = c }).ToList();
            var newCat = catPosted.ExceptBy(entity.Categories.Select(c => c.Name), c => c.Name).ToList();
            newCat.AddRange(entity.Categories.IntersectBy(post.Categories.ToList(), c => c.Name));
            entity.Categories = newCat;
        }

        private static Post MapEntityToPost(PostDb post)
        {
            if (post is null)
            {
                return null;
            }

            var postDto = new Post
            {
                ID = post.ID.ToString(),
                Content = post.Content,
                Excerpt = post.Excerpt,
                IsPublished = post.IsPublished,
                LastModified = post.LastModified,
                PubDate = post.PubDate,
                Slug = post.Slug,
                Title = post.Title
            };

            foreach (var comment in post.Comments)
            {
                postDto.Comments.Add(new Comment
                {
                    ID = comment.ID.ToString(),
                    Author = comment.Author,
                    Content = comment.Content,
                    Email = comment.Email,
                    IsAdmin = comment.IsAdmin,
                    PubDate= comment.PubDate
                });
            }

            foreach (var tag in post.Tags)
            {
                postDto.Tags.Add(tag.Name);
            }

            foreach (var cat in post.Categories)
            {
                postDto.Categories.Add(cat.Name);
            }

            return postDto;
        }
    }
}
