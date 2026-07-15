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
                "event_title, event_start_date, venue_name, fees_included " +
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
            receiptBuilder.Append("<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"margin-top:16px;border-top:1px solid #e5e7eb;padding-top:16px;\">");
            
            if (feesIncluded)
            {
                
                receiptBuilder.Append($"<tr><td style=\"font-size:15px;color:#374151;padding:4px 0;\"><strong>Total:</strong></td><td align=\"right\" style=\"font-size:15px;color:#374151;padding:4px 0;\"><strong>${totalCents / 100.0:F2}</strong></td></tr>");
            }
            else
            {
                
                receiptBuilder.Append($"<tr><td style=\"font-size:15px;color:#374151;padding:4px 0;\">Subtotal:</td><td align=\"right\" style=\"font-size:15px;color:#374151;padding:4px 0;\">${subtotalCents / 100.0:F2}</td></tr>");
                receiptBuilder.Append($"<tr><td style=\"font-size:15px;color:#374151;padding:4px 0;\">Fees:</td><td align=\"right\" style=\"font-size:15px;color:#374151;padding:4px 0;\">${feeCents / 100.0:F2}</td></tr>");
                receiptBuilder.Append($"<tr><td style=\"font-size:15px;color:#374151;padding:4px 0;\"><strong>Total:</strong></td><td align=\"right\" style=\"font-size:15px;color:#374151;padding:4px 0;\"><strong>${totalCents / 100.0:F2}</strong></td></tr>");
            }
            receiptBuilder.Append("</table>");

            
            var ticketBuilder = new StringBuilder();
            if (ticketsList.Count > 0)
            {
                ticketBuilder.Append("<h3 style=\"margin:24px 0 8px;font-size:16px;color:#111827;\">Your Tickets</h3>");
                ticketBuilder.Append("<ul style=\"margin:0;padding-left:20px;font-size:15px;color:#374151;\">");
                foreach (var ticket in ticketsList)
                {
                    ticketBuilder.Append($"<li style=\"margin-bottom:8px;\"><strong>Ticket Code:</strong> {ticket.code} (Seat: {ticket.seat})</li>");
                }
                ticketBuilder.Append("</ul>");
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
