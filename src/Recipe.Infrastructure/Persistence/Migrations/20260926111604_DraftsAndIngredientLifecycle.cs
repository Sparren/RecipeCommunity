using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Recipe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DraftsAndIngredientLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Recipe_Yield",
                table: "Recipes");

            migrationBuilder.AlterColumn<decimal>(
                name: "YieldValue",
                table: "Recipes",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<Guid>(
                name: "YieldUnitId",
                table: "Recipes",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "RecipeTypeId",
                table: "Recipes",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "Ingredients",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "Ingredients",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Ingredients",
                type: "text",
                nullable: true);

            // Before lifecycle metadata existed, all catalogue writes used the seeded administrator.
            // Historical creation times were not recorded; use the migration time, not invented history.
            migrationBuilder.Sql("""
                UPDATE "Ingredients" SET "Status" = 'Active',
                    "CreatedByUserId" = '10000000-0000-0000-0000-000000000001',
                    "CreatedAt" = CURRENT_TIMESTAMP;
                ALTER TABLE "Ingredients" ALTER COLUMN "Status" SET NOT NULL,
                    ALTER COLUMN "CreatedByUserId" SET NOT NULL,
                    ALTER COLUMN "CreatedAt" SET NOT NULL;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Recipe_Yield",
                table: "Recipes",
                sql: "(\"YieldValue\" IS NULL AND \"YieldMaximumValue\" IS NULL AND \"YieldUnitId\" IS NULL) OR (\"YieldValue\" IS NOT NULL AND \"YieldValue\" > 0 AND \"YieldUnitId\" IS NOT NULL AND (\"YieldMaximumValue\" IS NULL OR \"YieldMaximumValue\" > \"YieldValue\"))");

            migrationBuilder.CreateIndex(
                name: "IX_Ingredients_CreatedByUserId",
                table: "Ingredients",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Ingredients_Status_CreatedByUserId",
                table: "Ingredients",
                columns: new[] { "Status", "CreatedByUserId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Ingredient_Status",
                table: "Ingredients",
                sql: "\"Status\" IN ('Active', 'PendingReview')");

            migrationBuilder.AddForeignKey(
                name: "FK_Ingredients_Users_CreatedByUserId",
                table: "Ingredients",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "Recipes" WHERE "RecipeTypeId" IS NULL OR "YieldValue" IS NULL)
                        OR EXISTS (SELECT 1 FROM "Ingredients" WHERE "Status" = 'PendingReview') THEN
                        RAISE EXCEPTION 'Cannot downgrade while incomplete drafts or pending ingredients exist. Resolve them explicitly first.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_Ingredients_Users_CreatedByUserId",
                table: "Ingredients");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Recipe_Yield",
                table: "Recipes");

            migrationBuilder.DropIndex(
                name: "IX_Ingredients_CreatedByUserId",
                table: "Ingredients");

            migrationBuilder.DropIndex(
                name: "IX_Ingredients_Status_CreatedByUserId",
                table: "Ingredients");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Ingredient_Status",
                table: "Ingredients");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Ingredients");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "Ingredients");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Ingredients");

            migrationBuilder.AlterColumn<decimal>(
                name: "YieldValue",
                table: "Recipes",
                type: "numeric",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "YieldUnitId",
                table: "Recipes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "RecipeTypeId",
                table: "Recipes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Recipe_Yield",
                table: "Recipes",
                sql: "\"YieldValue\" > 0 AND (\"YieldMaximumValue\" IS NULL OR \"YieldMaximumValue\" > \"YieldValue\")");
        }
    }
}
