using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Npgsql;
using TicketSpan.Api.Data;

namespace TicketSpan.Api.Email;

public static class BookingEmailSender
{
    public static async Task SendBookingConfirmationEmailAsync(
        NpgsqlConnection conn,
        Guid bookingId,
        IEmailService emailService,
        EmailTemplateRenderer templates,
        AppSettingsProvider settings,
        ILogger logger,
        CancellationToken ct)
    {
        try
        {

            await using var cmd = new NpgsqlCommand(
                "SELECT booking_number, subtotal_cents, fee_cents, total_cents, user_email, " +
                "event_title, event_start_date, venue_name, fees_included, tax_cents, service_fee_cents " +
                "FROM vw_bookings WHERE bookings_id = @id", conn);
            cmd.Parameters.AddWithValue("id", bookingId);

            string bookingNumber = "";
            int subtotalCents = 0;
            int feeCents = 0;
            int totalCents = 0;
            string userEmail = "";
            string eventTitle = "";
            DateTime eventStartDate = DateTime.MinValue;
            string venueName = "";
            bool feesIncluded = false;
            int taxCents = 0;
            int serviceFeeCents = 0;

            await using (var reader = await cmd.ExecuteReaderAsync(ct))
            {
                if (await reader.ReadAsync(ct))
                {
                    bookingNumber = reader.GetString(0);
                    subtotalCents = reader.GetInt32(1);
                    feeCents = reader.GetInt32(2);
                    totalCents = reader.GetInt32(3);
                    userEmail = reader.GetString(4);
                    eventTitle = reader.GetString(5);
                    eventStartDate = reader.GetDateTime(6);
                    venueName = reader.GetString(7);
                    feesIncluded = reader.GetBoolean(8);
                    taxCents = reader.GetInt32(9);
                    serviceFeeCents = reader.GetInt32(10);
                }
            }

            if (string.IsNullOrEmpty(userEmail))
            {
                logger.LogWarning("Could not find booking user email for confirmation (BookingId: {BookingId})", bookingId);
                return;
            }

            var ticketsList = new List<(string code, int seat)>();
            await using (var ticketCmd = new NpgsqlCommand(
                "SELECT ticket_code, seat_number FROM vw_booking_ticket_lines WHERE bookings_id = @id ORDER BY seat_number", conn))
            {
                ticketCmd.Parameters.AddWithValue("id", bookingId);
                await using var reader = await ticketCmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    ticketsList.Add((reader.GetString(0), reader.GetInt32(1)));
                }
            }

            var receiptBuilder = new StringBuilder();
            receiptBuilder.Append("<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"border:1px solid #e2e8f0;border-radius:8px;background-color:#f8fafc;padding:12px 16px;\">");

            if (feesIncluded)
            {
                receiptBuilder.Append($"<tr><td style=\"font-size:14px;color:#64748b;padding:6px 0;\">Estimated Tax:</td><td align=\"right\" style=\"font-size:14px;color:#334155;padding:6px 0;\">${taxCents / 100.0:F2}</td></tr>");
                receiptBuilder.Append($"<tr><td style=\"font-size:15px;font-weight:700;color:#0f172a;padding:8px 0 2px;border-top:1px solid #e2e8f0;\">Total Paid:</td><td align=\"right\" style=\"font-size:16px;font-weight:700;color:#0f172a;padding:8px 0 2px;border-top:1px solid #e2e8f0;\">${totalCents / 100.0:F2}</td></tr>");
            }
            else
            {
                receiptBuilder.Append($"<tr><td style=\"font-size:14px;color:#64748b;padding:4px 0;\">Subtotal:</td><td align=\"right\" style=\"font-size:14px;color:#334155;padding:4px 0;\">${subtotalCents / 100.0:F2}</td></tr>");
                receiptBuilder.Append($"<tr><td style=\"font-size:14px;color:#64748b;padding:4px 0;\">Service fee:</td><td align=\"right\" style=\"font-size:14px;color:#334155;padding:4px 0;\">${serviceFeeCents / 100.0:F2}</td></tr>");
                receiptBuilder.Append($"<tr><td style=\"font-size:14px;color:#64748b;padding:4px 0;\">Tax:</td><td align=\"right\" style=\"font-size:14px;color:#334155;padding:4px 0;\">${taxCents / 100.0:F2}</td></tr>");
                receiptBuilder.Append($"<tr><td style=\"font-size:15px;font-weight:700;color:#0f172a;padding:8px 0 2px;border-top:1px solid #e2e8f0;\">Total Paid:</td><td align=\"right\" style=\"font-size:16px;font-weight:700;color:#0f172a;padding:8px 0 2px;border-top:1px solid #e2e8f0;\">${totalCents / 100.0:F2}</td></tr>");
            }
            receiptBuilder.Append("</table>");

            var ticketBuilder = new StringBuilder();
            if (ticketsList.Count > 0)
            {
                ticketBuilder.Append("<h3 style=\"margin:24px 0 12px;font-size:15px;font-weight:700;color:#0f172a;text-transform:uppercase;letter-spacing:0.5px;\">Your Tickets</h3>");
                ticketBuilder.Append("<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"margin-bottom:16px;\">");
                foreach (var ticket in ticketsList)
                {
                    ticketBuilder.Append("<tr><td style=\"padding:8px 12px;background:#ffffff;border:1px solid #e2e8f0;border-radius:6px;margin-bottom:6px;\">");
                    ticketBuilder.Append($"<span style=\"font-size:14px;font-weight:600;color:#0f172a;font-family:monospace;\">{ticket.code}</span>");
                    ticketBuilder.Append($"<span style=\"font-size:13px;color:#64748b;margin-left:12px;\">(Seat: {ticket.seat})</span>");
                    ticketBuilder.Append("</td></tr>");
                }
                ticketBuilder.Append("</table>");
            }

            var fromAddress = await settings.GetStringAsync("admin_invitation_email", "noreply@ticketspan.com", ct);
            var subject = $"Your Booking Confirmation: {bookingNumber}";

            var values = new Dictionary<string, string>
            {
                ["Subject"] = subject,
                ["EventTitle"] = eventTitle,
                ["EventDate"] = eventStartDate.ToString("f"),
                ["VenueName"] = venueName,
                ["BookingNumber"] = bookingNumber,
                ["ReceiptContent"] = receiptBuilder.ToString(),
                ["TicketDetails"] = ticketBuilder.ToString()
            };

            var htmlBody = await templates.RenderAsync("booking_confirmation.html", values, ct);
            await emailService.SendAsync(fromAddress, userEmail, subject, htmlBody, ct);
            logger.LogInformation("Booking confirmation email sent for BookingId: {BookingId}", bookingId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send booking confirmation email for BookingId: {BookingId}", bookingId);
        }
    }
}
