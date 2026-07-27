using Application.Common.Interfaces;
using Domain.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Infrastructure.Services;

public class QuestPdfInvoiceGenerator : IInvoicePdfGenerator
{
    public Task<byte[]> GenerateAsync(Invoice invoice, CancellationToken cancellationToken)
    {
        return Task.FromResult(GeneratePdf(invoice));
    }

    private byte[] GeneratePdf(Invoice invoice)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(12));

                page.Header()
                    .AlignCenter()
                    .Text($"INVOICE: {invoice.InvoiceNumber}")
                    .SemiBold().FontSize(20).FontColor(Colors.Blue.Darken2);

                page.Content()
                    .PaddingVertical(1, Unit.Centimetre)
                    .Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(col2 =>
                            {
                                col2.Item().Text($"Invoice Number: {invoice.InvoiceNumber}");
                                col2.Item().Text($"Date: {invoice.IssuedAt:dd/MM/yyyy HH:mm}");
                                col2.Item().Text($"Order: {invoice.Order?.OrderNumber ?? invoice.OrderId.ToString()[..8]}");
                                if (invoice.Order?.Country != null)
                                    col2.Item().Text($"Country: {invoice.Order.Country.Name}");
                            });

                            row.RelativeItem().Column(col2 =>
                            {
                                col2.Item().Text("Customer Information:").Bold();
                                if (invoice.Order?.Customer != null)
                                {
                                    col2.Item().Text($"Name: {invoice.Order.Customer.Name}");
                                    col2.Item().Text($"Email: {invoice.Order.Customer.Email}");
                                    col2.Item().Text($"Phone: {invoice.Order.Customer.Phone}");
                                }
                                else if (invoice.Order != null)
                                {
                                    // Guest order
                                    var guestName = invoice.Order.GuestName ?? invoice.Order.Phone ?? "Guest";
                                    var guestPhone = invoice.Order.GuestPhone ?? invoice.Order.Phone ?? "-";
                                    var guestAddr = invoice.Order.GuestAddress ?? invoice.Order.Address ?? "-";
                                    col2.Item().Text($"Name: {guestName}");
                                    col2.Item().Text($"Phone: {guestPhone}");
                                    col2.Item().Text($"Address: {guestAddr}");
                                }
                                else
                                {
                                    col2.Item().Text("Guest Order");
                                }
                            });
                        });


                        col.Item().PaddingTop(1, Unit.Centimetre).LineHorizontal(1);

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(3);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(2);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Product").Bold();
                                header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Qty").Bold().AlignRight();
                                header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Unit Price").Bold().AlignRight();
                                header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Total").Bold().AlignRight();
                            });

                            if (invoice.Order?.OrderItems != null && invoice.Order.OrderItems.Any())
                            {
                                foreach (var item in invoice.Order.OrderItems)
                                {
                                    var totalPrice = item.Quantity * item.PriceAtOrder;
                                    
                                    table.Cell().Padding(5).Text(item.Product?.NameEn ?? "Product");
                                    table.Cell().Padding(5).Text(item.Quantity.ToString()).AlignRight();
                                    table.Cell().Padding(5).Text($"{item.PriceAtOrder:C}").AlignRight();
                                    table.Cell().Padding(5).Text($"{totalPrice:C}").AlignRight();
                                }
                            }
                            else
                            {
                                table.Cell().Padding(5).Text("No items loaded");
                                table.Cell().Padding(5).Text("-");
                                table.Cell().Padding(5).Text("-");
                                table.Cell().Padding(5).Text("-");
                            }
                        });

                        col.Item().PaddingTop(1, Unit.Centimetre)
                            .AlignRight()
                            .Text($"Total Amount: {invoice.TotalAmount:C}")
                            .SemiBold().FontSize(16).FontColor(Colors.Green.Darken2);
                    });

                page.Footer()
                    .AlignCenter()
                    .Text("Thank you for your business!")
                    .FontSize(10).FontColor(Colors.Grey.Medium);
            });
        });

        return document.GeneratePdf();
    }
}
