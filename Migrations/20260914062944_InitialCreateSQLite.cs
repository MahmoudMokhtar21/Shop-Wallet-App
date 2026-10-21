using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShopWalletApp.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreateSQLite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppSettings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSettings", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "MasterNumbers",
                columns: table => new
                {
                    PhoneNumber = table.Column<string>(type: "TEXT", maxLength: 11, nullable: false),
                    DailyLimit = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    WeeklyLimit = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    MonthlyLimit = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MasterNumbers", x => x.PhoneNumber);
                });

            migrationBuilder.CreateTable(
                name: "Transactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PhoneNumber = table.Column<string>(type: "TEXT", maxLength: 11, nullable: false),
                    SourcePhone = table.Column<string>(type: "TEXT", maxLength: 11, nullable: true),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TransactionType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transactions", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "AppSettings",
                columns: new[] { "Key", "Value" },
                values: new object[] { "AlertThresholdAmount", "1000" });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_SourcePhone_TransactionType_TransactionDate",
                table: "Transactions",
                columns: new[] { "SourcePhone", "TransactionType", "TransactionDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppSettings");

            migrationBuilder.DropTable(
                name: "MasterNumbers");

            migrationBuilder.DropTable(
                name: "Transactions");
        }
    }
}
