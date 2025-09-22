using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;

namespace AgentsDemoSK.Tools;

/// <summary>
/// Tool for sending email confirmations
/// </summary>
public class EmailTool : ITool
{
    // In a real implementation, this would connect to an email service
    private readonly List<Dictionary<string, string>> _sentEmails = new();

    public string Name => "EmailTool";

    /// Sends a confirmation email to the user
    [KernelFunction, Description("Sends a confirmation email to the user.")]
    public Task<string> SendConfirmationEmail(
        [Description("The recipient's email address")] string emailAddress,
        [Description("The action that was completed (e.g., 'appointment booking')")] string action,
        [Description("Details about the action")] string details,
        [Description("User feedback (optional)")] string? feedback = null)
    {
        // Create email content
        string currentDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        string emailContent = $"Dear Patient,\n\nThis is a confirmation that your {action} has been successfully completed.\n\nDetails: {details}\n\nDate: {currentDate}";
        
        if (!string.IsNullOrEmpty(feedback))
        {
            emailContent += $"\n\nYour feedback: {feedback}\n\nThank you for your input. We value your opinion and will use it to improve our services.";
        }
        
        emailContent += "\n\nIf you have any questions, please contact our customer service.\n\nBest regards,\nYour Healthcare Provider";
        
        // In a real implementation, this would actually send an email
        // For now, we'll just store it
        var emailRecord = new Dictionary<string, string>
        {
            ["recipient"] = emailAddress,
            ["subject"] = $"Confirmation: {action}",
            ["content"] = emailContent,
            ["timestamp"] = currentDate
        };
        
        _sentEmails.Add(emailRecord);
        
        // Return result
        var result = new
        {
            success = true,
            message = $"Confirmation email for {action} sent to {emailAddress}",
            email_id = _sentEmails.Count
        };
        
        return Task.FromResult(JsonSerializer.Serialize(result));
    }
}