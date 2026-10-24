using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaPosts.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPostSlugRedirects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PostSlugRedirects",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Slug = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    PostId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostSlugRedirects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PostSlugRedirects_Posts_PostId",
                        column: x => x.PostId,
                        principalTable: "Posts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PostSlugRedirects_PostId",
                table: "PostSlugRedirects",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_PostSlugRedirects_Slug",
                table: "PostSlugRedirects",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PostSlugRedirects");
        }
    }
}
