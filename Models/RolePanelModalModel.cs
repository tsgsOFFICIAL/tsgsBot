using Discord.Interactions;
using Discord;

namespace tsgsBot_C_.Models
{
    public sealed class RolePanelModalModel : IModal
    {
        public string Title => "Edit Role Panel";

        [InputLabel("Title")]
        [ModalTextInput("title", TextInputStyle.Short, placeholder: "Self-assign Roles")]
        public required string PanelTitle { get; set; }

        [InputLabel("Description")]
        [ModalTextInput("description", TextInputStyle.Paragraph, placeholder: "Select your roles below")]
        public required string Description { get; set; }

        [InputLabel("Button Labels (one per line, must match role count)")]
        [ModalTextInput("button_labels", TextInputStyle.Paragraph, placeholder: "Role 1\nRole 2\nRole 3")]
        public string? ButtonLabels { get; set; }
    }
}
