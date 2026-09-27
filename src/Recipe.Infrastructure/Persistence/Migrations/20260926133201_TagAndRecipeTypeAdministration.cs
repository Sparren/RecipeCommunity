using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Recipe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TagAndRecipeTypeAdministration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Tags",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "TagName",
                type: "text",
                nullable: false,
                collation: "recipe_text",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "RecipeTypes",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "RecipeTypes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddCheckConstraint(
                name: "CK_RecipeType_SortOrder",
                table: "RecipeTypes",
                sql: "\"SortOrder\" >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_RecipeType_SortOrder",
                table: "RecipeTypes");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Tags");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "RecipeTypes");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "RecipeTypes");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "TagName",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldCollation: "recipe_text");
        }
    }
}
