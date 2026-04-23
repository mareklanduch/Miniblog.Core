using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Miniblog.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddSlugIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Posts_Slug",
                table: "Posts",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Posts_Slug",
                table: "Posts");
        }
    }
}
