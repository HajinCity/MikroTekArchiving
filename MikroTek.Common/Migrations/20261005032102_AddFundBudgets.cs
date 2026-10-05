using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MikroTek.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddFundBudgets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FundBudgets",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Department = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    FundType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FundBudgets", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FundBudgets_Year_Department_FundType",
                table: "FundBudgets",
                columns: new[] { "Year", "Department", "FundType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FundBudgets");
        }
    }
}
