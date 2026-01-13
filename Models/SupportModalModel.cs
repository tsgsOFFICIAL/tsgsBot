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
}