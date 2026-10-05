using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MikroTek.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddDisbursementArchive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CashAdvances",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Department = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    FundType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BatchNo = table.Column<int>(type: "int", nullable: false),
                    PayrollNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AdaNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Date = table.Column<DateTime>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Period = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FundCluster = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Officer = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    OfficerPosition = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    VerifiedBy = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    VerifiedByPosition = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashAdvances", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChecklistTemplates",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChecklistTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Payees",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Tin = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TaxType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payees", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SchoolProfiles",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Region = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Division = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    District = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    JhsBankAccount = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ShsBankAccount = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ResponsibilityCenter = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    FundCluster = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    HeadName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    HeadPosition = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    BookkeeperName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    BookkeeperPosition = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ValidatorName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ValidatorPosition = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    CashUnitName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    CashUnitPosition = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchoolProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DisbursementVouchers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CashAdvanceId = table.Column<long>(type: "bigint", nullable: false),
                    IsDraft = table.Column<bool>(type: "bit", nullable: false),
                    DvNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Date = table.Column<DateTime>(type: "date", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ChecklistTemplateId = table.Column<long>(type: "bigint", nullable: true),
                    Payee = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Tin = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Particulars = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    FundCluster = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ResponsibilityCenter = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    MfoPap = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    OrsBurs = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Tax1 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Tax3 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Mode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    CheckNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    PaidDate = table.Column<DateTime>(type: "date", nullable: true),
                    Bank = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ReceivedBy = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    OrNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    JevNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ChecklistRemarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DisbursementVouchers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DisbursementVouchers_CashAdvances_CashAdvanceId",
                        column: x => x.CashAdvanceId,
                        principalTable: "CashAdvances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ChecklistTemplateItems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TemplateId = table.Column<long>(type: "bigint", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChecklistTemplateItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChecklistTemplateItems_ChecklistTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "ChecklistTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DvChecklistItems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DvId = table.Column<long>(type: "bigint", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Mark = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DateText = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DvChecklistItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DvChecklistItems_DisbursementVouchers_DvId",
                        column: x => x.DvId,
                        principalTable: "DisbursementVouchers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DvLines",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DvId = table.Column<long>(type: "bigint", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DvLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DvLines_DisbursementVouchers_DvId",
                        column: x => x.DvId,
                        principalTable: "DisbursementVouchers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CashAdvances_PayrollNo",
                table: "CashAdvances",
                column: "PayrollNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChecklistTemplateItems_TemplateId",
                table: "ChecklistTemplateItems",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_ChecklistTemplates_Name",
                table: "ChecklistTemplates",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DisbursementVouchers_CashAdvanceId",
                table: "DisbursementVouchers",
                column: "CashAdvanceId");

            migrationBuilder.CreateIndex(
                name: "IX_DisbursementVouchers_DvNo",
                table: "DisbursementVouchers",
                column: "DvNo",
                unique: true,
                filter: "[DvNo] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DvChecklistItems_DvId",
                table: "DvChecklistItems",
                column: "DvId");

            migrationBuilder.CreateIndex(
                name: "IX_DvLines_DvId",
                table: "DvLines",
                column: "DvId");

            migrationBuilder.CreateIndex(
                name: "IX_Payees_Name",
                table: "Payees",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChecklistTemplateItems");

            migrationBuilder.DropTable(
                name: "DvChecklistItems");

            migrationBuilder.DropTable(
                name: "DvLines");

            migrationBuilder.DropTable(
                name: "Payees");

            migrationBuilder.DropTable(
                name: "SchoolProfiles");

            migrationBuilder.DropTable(
                name: "ChecklistTemplates");

            migrationBuilder.DropTable(
                name: "DisbursementVouchers");

            migrationBuilder.DropTable(
                name: "CashAdvances");
        }
    }
}
