using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wallets.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialWallets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "wallets");

            migrationBuilder.CreateTable(
                name: "wallets",
                schema: "wallets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wallets", x => x.id);
                    table.CheckConstraint("ck_wallets_currency", "currency = 'USD'");
                    table.CheckConstraint("ck_wallets_id", "id <> '00000000-0000-0000-0000-000000000000'::uuid");
                    table.CheckConstraint("ck_wallets_owner", "owner_id <> '00000000-0000-0000-0000-000000000000'::uuid");
                    table.CheckConstraint("ck_wallets_status", "status IN ('Active', 'Closed')");
                });

            migrationBuilder.CreateIndex(
                name: "ix_wallets_owner_id",
                schema: "wallets",
                table: "wallets",
                column: "owner_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "wallets",
                schema: "wallets");
        }
    }
}
