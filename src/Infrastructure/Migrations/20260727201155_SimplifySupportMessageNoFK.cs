using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SimplifySupportMessageNoFK : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SupportMessages_SupportConversations_ConversationId",
                table: "SupportMessages");

            migrationBuilder.DropForeignKey(
                name: "FK_SupportMessages_Users_SenderId",
                table: "SupportMessages");

            migrationBuilder.DropIndex(
                name: "IX_SupportConversations_OrderNumber",
                table: "SupportConversations");

            migrationBuilder.DropColumn(
                name: "AttachmentUrl",
                table: "SupportMessages");

            migrationBuilder.DropColumn(
                name: "GuestName",
                table: "SupportMessages");

            migrationBuilder.DropColumn(
                name: "RecipientId",
                table: "SupportMessages");

            migrationBuilder.DropColumn(
                name: "RelatedOrderId",
                table: "SupportMessages");

            migrationBuilder.DropColumn(
                name: "RelatedReviewId",
                table: "SupportMessages");

            migrationBuilder.RenameColumn(
                name: "SenderId",
                table: "SupportMessages",
                newName: "SupportConversationId");

            migrationBuilder.RenameIndex(
                name: "IX_SupportMessages_SenderId",
                table: "SupportMessages",
                newName: "IX_SupportMessages_SupportConversationId");

            migrationBuilder.AddColumn<string>(
                name: "SenderName",
                table: "SupportMessages",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "Customer");

            migrationBuilder.AddColumn<string>(
                name: "SenderType",
                table: "SupportMessages",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Customer");

            migrationBuilder.AddForeignKey(
                name: "FK_SupportMessages_SupportConversations_SupportConversationId",
                table: "SupportMessages",
                column: "SupportConversationId",
                principalTable: "SupportConversations",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SupportMessages_SupportConversations_SupportConversationId",
                table: "SupportMessages");

            migrationBuilder.DropColumn(
                name: "SenderName",
                table: "SupportMessages");

            migrationBuilder.DropColumn(
                name: "SenderType",
                table: "SupportMessages");

            migrationBuilder.RenameColumn(
                name: "SupportConversationId",
                table: "SupportMessages",
                newName: "SenderId");

            migrationBuilder.RenameIndex(
                name: "IX_SupportMessages_SupportConversationId",
                table: "SupportMessages",
                newName: "IX_SupportMessages_SenderId");

            migrationBuilder.AddColumn<string>(
                name: "AttachmentUrl",
                table: "SupportMessages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuestName",
                table: "SupportMessages",
                type: "text",
                nullable: true);

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

            migrationBuilder.CreateIndex(
                name: "IX_SupportConversations_OrderNumber",
                table: "SupportConversations",
                column: "OrderNumber",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_SupportMessages_SupportConversations_ConversationId",
                table: "SupportMessages",
                column: "ConversationId",
                principalTable: "SupportConversations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SupportMessages_Users_SenderId",
                table: "SupportMessages",
                column: "SenderId",
                principalTable: "Users",
                principalColumn: "Id");
        }
    }
}
