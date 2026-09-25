using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Recipe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UnicodeSearchCollation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:CollationDefinition:recipe_text", "und,und,icu,True");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Recipes",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                collation: "recipe_text",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "IngredientName",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                collation: "recipe_text",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "IngredientAlias",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                collation: "recipe_text",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Recipes",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldCollation: "recipe_text");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "IngredientName",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldCollation: "recipe_text");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "IngredientAlias",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldCollation: "recipe_text");

            // Columns must stop using the collation before PostgreSQL can drop it.
            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:CollationDefinition:recipe_text", "und,und,icu,True");
        }
    }
}
