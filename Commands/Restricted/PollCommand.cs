using tsgsBot_C_.StateServices;
using Discord.Interactions;
using tsgsBot_C_.Models;
using Discord.Rest;
using Discord;

namespace tsgsBot_C_.Commands.Restricted
{
    public sealed class PollCommand(PollFormStateService stateService) : LoggedCommandModule
    {
        [SlashCommand("poll", "Create a poll with a GUI")]
        [CommandContextType(InteractionContextType.Guild)]
        [DefaultMemberPermissions(GuildPermission.SendPolls)]
        public async Task PollAsync()
        {
            stateService.Clear(Context.User.Id); // Clear any previous state for this user

            // STEP 1: Duration select menu
            SelectMenuBuilder durationMenu = new SelectMenuBuilder()
                .WithCustomId("poll_duration")
                .WithPlaceholder("How long should the poll last?")
                .AddOption("1 minute", "1")
                .AddOption("5 minutes", "5")
                .AddOption("15 minutes", "15")
                .AddOption("30 minutes", "30")
                .AddOption("1 hour", "60")
                .AddOption("3 hours", "180")
                .AddOption("6 hours", "360")
                .AddOption("12 hours", "720")
                .AddOption("24 hours", "1440")
                .AddOption("3 days", "4320")
                .AddOption("1 week", "10080");

            ComponentBuilder durationRow = new ComponentBuilder().WithSelectMenu(durationMenu);

            await RespondAsync("First, select the duration of your poll:",
                components: durationRow.Build(),
                ephemeral: true);

            await LogCommandAsync();
        }

        [ComponentInteraction("poll_duration")]
        public async Task HandleDurationAsync(string[] values)
        {
            await DeferAsync(ephemeral: true);

            if (values.Length == 0)
                return;

            UserPollFormState state = stateService.GetOrCreate(Context.User.Id);
            state.DurationMinutes = int.Parse(values[0]);

            ComponentBuilder continueBtn = new ComponentBuilder()
                .WithButton("Continue →", "poll_continue", ButtonStyle.Primary);

            await ModifyOriginalResponseAsync(msg =>
            {
                msg.Content = $"Duration set to {state.DurationMinutes} minutes.\nClick Continue to set up your poll.";
                msg.Components = continueBtn.Build();
                msg.Flags = MessageFlags.Ephemeral;
            });
        }

        [ComponentInteraction("poll_continue")]
        public async Task HandleContinueAsync()
        {
            ModalBuilder modal = new ModalBuilder()
                .WithCustomId("poll_modal")
                .WithTitle("Create Your Poll")
                .AddTextInput("Poll Question", "question", TextInputStyle.Short, placeholder: "Are we raiding tonight?", required: true)
                .AddTextInput("Answers (one per line, 2–10)", "answers", TextInputStyle.Paragraph, placeholder: "Yes definitely!\nNo way\nOnly if bribed", required: true)
                .AddTextInput("Custom Emojis (one per line, optional)", "emojis", TextInputStyle.Paragraph, placeholder: ":pepe:\n🤨\n:banhammer:", required: false);

            await RespondWithModalAsync(modal.Build());
            await DeleteOriginalResponseAsync(); // Clean up the previous message
        }

        [ModalInteraction("poll_modal")]
        public async Task HandleModalSubmittedAsync(PollModalModel modal)
        {
            await DeferAsync(ephemeral: true);

            UserPollFormState state = stateService.GetOrCreate(Context.User.Id);

            string question = modal.Question.Trim();
            List<string> answers = modal.Answers.Trim().Split('\n').Select(a => a.Trim()).Where(a => !string.IsNullOrEmpty(a)).ToList();
            if (answers.Count < 2 || answers.Count > 10)
            {
                await ModifyOriginalResponseAsync(msg =>
                {
                    msg.Content = "You must provide 2–10 answers.";
                    msg.Components = new ComponentBuilder().Build(); // Clear components
                    msg.Flags = MessageFlags.Ephemeral;
                });

                return;
            }

            List<string> rawEmojis = modal.Emojis?.Trim().Split('\n').Select(e => e.Trim()).Where(e => !string.IsNullOrEmpty(e)).ToList() ?? new List<string>();

            // Parse emojis (guild custom or unicode)
            List<string> emojis = [.. rawEmojis.Select(emoji =>
            {
                if (emoji.StartsWith(':') && emoji.EndsWith(':') && Context.Guild != null)
                {
                    string name = emoji.Trim(':');
                    GuildEmote? guildEmoji = Context.Guild.Emotes.FirstOrDefault(em => em.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                    if (guildEmoji != null)
                        return guildEmoji.ToString(); // <:name:id>
                }

                return emoji;
            })];

            // Fill with defaults if missing
            string[] defaultEmojis = ["1️⃣", "2️⃣", "3️⃣", "4️⃣", "5️⃣", "6️⃣", "7️⃣", "8️⃣", "9️⃣", "🔟"];

            while (emojis.Count < answers.Count)
            {
                emojis.Add(defaultEmojis[emojis.Count]);
            }

            emojis = [.. emojis.Distinct()];

            // Preview embed
            EmbedBuilder previewEmbed = new EmbedBuilder()
                .WithTitle("📊 Poll (Preview)")
                .WithDescription(
                    $"{question}\n\n" +
                    string.Join("\n", answers.Select((a, i) => $"{emojis[i]} {a}")) +
                    $"\n\n⏳ Ends: <t:{DateTimeOffset.Now.AddMinutes(state.DurationMinutes).ToUnixTimeSeconds()}:R>")
                .WithColor(Color.Teal);

            ComponentBuilder confirmRow = new ComponentBuilder()
                .WithButton("Create Poll", "poll_confirm", ButtonStyle.Success)
                .WithButton("Cancel", "poll_cancel", ButtonStyle.Danger);

            await ModifyOriginalResponseAsync(msg =>
            {
                msg.Content = "Does this look good?";
                msg.Embed = previewEmbed.Build();
                msg.Components = confirmRow.Build();
                msg.Flags = MessageFlags.Ephemeral;
            });

            state.ModalData = modal;
        }

        [ComponentInteraction("poll_confirm")]
        public async Task HandleConfirmAsync()
        {
            await DeferAsync();

            UserPollFormState state = stateService.GetOrCreate(Context.User.Id);

            string[] answers = state.ModalData!.Answers.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            List<string> rawEmojis = state.ModalData!.Emojis?.Trim().Split('\n').Select(e => e.Trim()).Where(e => !string.IsNullOrEmpty(e)).ToList() ?? new List<string>();

            // Parse emojis (guild custom or unicode)
            List<string> emojis = [.. rawEmojis.Select(emoji =>
            {
                if (emoji.StartsWith(':') && emoji.EndsWith(':') && Context.Guild != null)
                {
                    string name = emoji.Trim(':');
                    GuildEmote? guildEmoji = Context.Guild.Emotes.FirstOrDefault(em => em.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                    if (guildEmoji != null)
                        return guildEmoji.ToString(); // <:name:id>
                }

                return emoji;
            })];

            // Fill with defaults if missing
            string[] defaultEmojis = ["1️⃣", "2️⃣", "3️⃣", "4️⃣", "5️⃣", "6️⃣", "7️⃣", "8️⃣", "9️⃣", "🔟"];

            while (emojis.Count < answers.Length)
            {
                emojis.Add(defaultEmojis[emojis.Count]);
            }

            emojis = [.. emojis.Distinct()];

            EmbedBuilder pollEmbed = new EmbedBuilder()
                .WithTitle("📊 Poll")
                .WithDescription(
                    $"{state.ModalData.Question}\n\n" +
                    string.Join("\n", answers.Select((a, i) => $"{emojis[i]} {a}")) +
                    $"\n\n⏳ Ends: <t:{DateTimeOffset.Now.AddMinutes(state.DurationMinutes).ToUnixTimeSeconds()}:R>")
                .WithColor(Color.Teal);

            RestUserMessage pollMessage = await Context.Channel.SendMessageAsync(embed: pollEmbed.Build());
            await DeleteOriginalResponseAsync(); // Clean up the ephemeral message

            foreach (string emoji in emojis)
            {
                IEmote emote = Emote.TryParse(emoji, out Emote? parsed) ? parsed : new Emoji(emoji);
                await pollMessage.AddReactionAsync(emote);
            }

            // Wait for duration and finalize
            await Task.Delay(TimeSpan.FromMinutes(state.DurationMinutes));

            // Finalize (count votes)
            pollMessage = (RestUserMessage)await Context.Channel.GetMessageAsync(pollMessage.Id);

            // Map emoji -> answer
            var options = emojis
                .Select((emoji, i) =>
                {
                    IEmote emote = Emote.TryParse(emoji, out Emote? parsed)
                        ? parsed
                        : new Emoji(emoji);

                    pollMessage.Reactions.TryGetValue(emote, out ReactionMetadata reaction);

                    int count = reaction.ReactionCount;
                    if (count > 0)
                        count--; // subtract bot reaction

                    return new
                    {
                        Emote = emote,
                        Answer = answers[i],
                        Count = count
                    };
                })
                .ToList();

            // Totals + ordering
            int totalVotes = options.Sum(o => o.Count);
            options = options.OrderByDescending(o => o.Count).ToList();

            // Build result lines
            IEnumerable<string> resultLines = options.Select((item, index) =>
            {
                double pct = totalVotes > 0
                    ? (double)item.Count / totalVotes * 100
                    : 0;

                string bar = BuildBar(pct);

                string line =
                    $"{item.Emote} **{item.Answer}**\n" +
                    $"     ┗ {item.Count,3} votes ({pct:0.0}%) {bar}";

                // Winner / tie markers
                if (index == 0 && item.Count > 0)
                {
                    if (options.Count > 1 && options[1].Count == item.Count)
                        line += " 🤝 **TIE**";
                    else
                        line += " 👑 **WINNER**";
                }

                return line;
            });

            // Build final embed
            EmbedBuilder resultEmbed = new EmbedBuilder()
                .WithTitle("📊 Poll Ended – Final Results")
                .WithDescription(
                    $"**{state.ModalData!.Question}**\n\n" +
                    string.Join("\n", resultLines) +
                    $"\n\n**Total votes:** {totalVotes}"
                )
                .WithColor(totalVotes > 0 ? Color.Green : Color.DarkRed)
                .WithCurrentTimestamp();

            await pollMessage.DeleteAsync();
            await Context.Channel.SendMessageAsync(embed: resultEmbed.Build()); // Send results
        }

        [ComponentInteraction("poll_cancel")]
        public async Task HandleCancelAsync()
        {
            await DeferAsync();
            await ModifyOriginalResponseAsync(msg =>
            {
                msg.Content = "Poll creation cancelled.";
                msg.Embed = null;
                msg.Components = new ComponentBuilder().Build(); // Clear components
                msg.Flags = MessageFlags.Ephemeral;
            });
        }

        private static string BuildBar(double pct)
        {
            int filled = (int)Math.Round(pct / 8.33); // 12 segments
            filled = Math.Clamp(filled, 0, 12);
            return new string('▰', filled) + new string('▱', 12 - filled);
        }
    }
}