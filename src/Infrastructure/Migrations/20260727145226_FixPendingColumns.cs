using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixPendingColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SupportMessages_Users_SenderId",
                table: "SupportMessages");

            migrationBuilder.DropColumn(
                name: "SenderName",
                table: "SupportMessages");

            migrationBuilder.DropColumn(
                name: "SenderRole",
                table: "SupportMessages");

            migrationBuilder.DropColumn(
                name: "GuestAddress",
                table: "Orders");

            migrationBuilder.RenameColumn(
                name: "GuestPhone",
                table: "Orders",
                newName: "Phone");

            migrationBuilder.RenameColumn(
                name: "GuestName",
                table: "Orders",
                newName: "Address");

            migrationBuilder.AlterColumn<Guid>(
                name: "SenderId",
                table: "SupportMessages",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RecipientId",
                table: "SupportMessages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RelatedOrderId",
                table: "SupportMessages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RelatedReviewId",
                table: "SupportMessages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                table: "Orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CustomerId",
                table: "Orders",
                column: "CustomerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Users_CustomerId",
                table: "Orders",
                column: "CustomerId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SupportMessages_Users_SenderId",
                table: "SupportMessages",
                column: "SenderId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Users_CustomerId",
                table: "Orders");

            migrationBuilder.DropForeignKey(
                name: "FK_SupportMessages_Users_SenderId",
                table: "SupportMessages");

            migrationBuilder.DropIndex(
                name: "IX_Orders_CustomerId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "RecipientId",
                table: "SupportMessages");

            migrationBuilder.DropColumn(
                name: "RelatedOrderId",
                table: "SupportMessages");

            migrationBuilder.DropColumn(
                name: "RelatedReviewId",
                table: "SupportMessages");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "Orders");

            migrationBuilder.RenameColumn(
                name: "Phone",
                table: "Orders",
                newName: "GuestPhone");

            migrationBuilder.RenameColumn(
                name: "Address",
                table: "Orders",
                newName: "GuestName");

            migrationBuilder.AlterColumn<Guid>(
                name: "SenderId",
                table: "SupportMessages",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "SenderName",
                table: "SupportMessages",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SenderRole",
                table: "SupportMessages",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "GuestAddress",
                table: "Orders",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddForeignKey(
                name: "FK_SupportMessages_Users_SenderId",
                table: "SupportMessages",
                column: "SenderId",
                principalTable: "Users",
                principalColumn: "Id");
        }
    }
}
