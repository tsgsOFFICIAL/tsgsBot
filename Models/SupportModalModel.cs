using Discord.Interactions;
using Discord;

namespace tsgsBot_C_.Models
{
    /// <summary>
    /// Represents the data model for a support request modal form, containing fields for issue description, operating
    /// system, version, reproduction steps, and additional information.
    /// </summary>
    /// <remarks>This model is typically used to collect detailed information from users when submitting a
    /// support request through a modal dialog. All properties are required and should be populated to ensure
    /// comprehensive issue reporting.</remarks>
    public class SupportModalModel : IModal
    {
        public string Title => "Support Form";

        [InputLabel("Describe the Issue")]
        [ModalTextInput("description", TextInputStyle.Paragraph)]
        public required string Description { get; set; }

        [InputLabel("Operating System")]
        [ModalTextInput("os", TextInputStyle.Short)]
        public required string OS { get; set; }

        [InputLabel("Version")]
        [ModalTextInput("version", TextInputStyle.Short)]
        public required string Version { get; set; }

        [InputLabel("Steps to Reproduce")]
        [ModalTextInput("steps", TextInputStyle.Paragraph)]
        public required string Steps { get; set; }

        [InputLabel("Additional Info / Logs")]
        [ModalTextInput("additional", TextInputStyle.Paragraph)]
        public required string Additional { get; set; }
    }
    /// <summary>
    /// Represents the state of a user support form, including selected application, issue details, and metadata.
    /// </summary>
    /// <remarks>This class is typically used to capture and track user input when submitting a support
    /// request. The properties correspond to form fields and metadata relevant for processing or expiring the form.
    /// Instances may be considered incomplete until the required fields, such as the selected application, are
    /// provided.</remarks>
    public class UserSupportFormState
    {
        public string? SelectedApp { get; set; } // "cs2aa" or "sdc"
        public string? IssueType { get; set; }
        public string? Reproducibility { get; set; }
        public string? Urgency { get; set; }
        public string? Platform { get; set; }

        // used as a timestamp for cleanup (e.g. expire after 30 min)
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        public bool IsComplete => !string.IsNullOrEmpty(SelectedApp);
    }
}