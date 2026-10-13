using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MikroTek.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddVatWithholding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ManualTax",
                table: "DisbursementVouchers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PayeeTaxType",
                table: "DisbursementVouchers",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "NonVat");

            migrationBuilder.AddColumn<decimal>(
                name: "Tax5",
                table: "DisbursementVouchers",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxBase",
                table: "DisbursementVouchers",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ManualTax",
                table: "DisbursementVouchers");

            migrationBuilder.DropColumn(
                name: "PayeeTaxType",
                table: "DisbursementVouchers");

            migrationBuilder.DropColumn(
                name: "Tax5",
                table: "DisbursementVouchers");

            migrationBuilder.DropColumn(
                name: "TaxBase",
                table: "DisbursementVouchers");
        }
    }
}
