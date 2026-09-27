using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Recipe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CatalogueAliases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "RecipeTypeName",
                type: "text",
                nullable: false,
                collation: "recipe_text",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateTable(
                name: "RecipeTypeAlias",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipeTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Language = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, collation: "recipe_text")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeTypeAlias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecipeTypeAlias_RecipeTypes_RecipeTypeId",
                        column: x => x.RecipeTypeId,
                        principalTable: "RecipeTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TagAlias",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TagId = table.Column<Guid>(type: "uuid", nullable: false),
                    Language = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, collation: "recipe_text")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TagAlias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TagAlias_Tags_TagId",
                        column: x => x.TagId,
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecipeTypeAlias_RecipeTypeId",
                table: "RecipeTypeAlias",
                column: "RecipeTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_TagAlias_TagId",
                table: "TagAlias",
                column: "TagId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecipeTypeAlias");

            migrationBuilder.DropTable(
                name: "TagAlias");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "RecipeTypeName",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldCollation: "recipe_text");
        }
    }
}
