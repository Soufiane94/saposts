using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaPosts.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaVariants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasAlpha",
                table: "Media",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsAnimated",
                table: "Media",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Widths",
                table: "Media",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasAlpha",
                table: "Media");

            migrationBuilder.DropColumn(
                name: "IsAnimated",
                table: "Media");

            migrationBuilder.DropColumn(
                name: "Widths",
                table: "Media");
        }
    }
}
