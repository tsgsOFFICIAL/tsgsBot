using Discord.Interactions;
using Discord;

namespace tsgsBot_C_.Models
{
    public sealed class PollModalModel : IModal
    {
        public string Title => "Create Your Poll";

        [InputLabel("Poll Question")]
        [ModalTextInput("question", TextInputStyle.Short)]
        public required string Question { get; set; }

        [InputLabel("Answers (one per line, 2–10)")]
        [ModalTextInput("answers", TextInputStyle.Paragraph)]
        public required string Answers { get; set; }

        [InputLabel("Emojis (one per line, optional)")]
        [ModalTextInput("emojis", TextInputStyle.Paragraph)]
        public string Emojis { get; set; } = string.Empty;
    }
}