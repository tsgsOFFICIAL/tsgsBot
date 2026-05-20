using tsgsBot_C_.StateServices;
using Discord.Interactions;
using Discord.WebSocket;
using Discord;
using tsgsBot_C_.Models;
using tsgsBot_C_.Services;

namespace tsgsBot_C_.Bot.Commands.ContextMenuCommands
{
    /// <summary>
    /// Context menu command for editing existing giveaway messages.
    /// Loads giveaway data from the database and opens the giveaway editor modal.
    /// </summary>
    public sealed class EditGiveawayContextMenuCommand(GiveawayFormStateService stateService, ILogger<EditGiveawayContextMenuCommand> logger) : LoggedCommandModule
    {
        private readonly GiveawayFormStateService _stateService = stateService;
        private readonly ILogger<EditGiveawayContextMenuCommand> _logger = logger;

        [MessageCommand("Edit Giveaway")]
        [CommandContextType(InteractionContextType.Guild)]
        [DefaultMemberPermissions(GuildPermission.CreateEvents)]
        public async Task EditGiveawayMessageAsync(IMessage targetMessage)
        {
            _logger.LogInformation("Edit giveaway command invoked by user {UserId} on message {MessageId}", Context.User.Id, targetMessage.Id);

            if (targetMessage is not IUserMessage userMessage || userMessage.Embeds.Count == 0)
            {
                await RespondAsync("Target message must be a giveaway message with an embed.", ephemeral: true);
                return;
            }

            DatabaseGiveawayModel? giveaway = await DatabaseService.Instance.GetGiveawayByMessageIdAsync(targetMessage.Id.ToString());
            if (giveaway == null)
            {
                await RespondAsync("Message does not appear to be a registered giveaway.", ephemeral: true);
                return;
            }

            if (giveaway.HasEnded)
            {
                await RespondAsync("This giveaway has already ended and cannot be edited.", ephemeral: true);
                return;
            }

            UserGiveawayFormState state = _stateService.GetOrCreate(Context.User.Id);
            state.ModalData = new GiveawayModalModel
            {
                Prize = giveaway.Prize,
                Winners = giveaway.Winners.ToString(),
                ReactionEmoji = giveaway.ReactionEmoji
            };
            state.ImageUrl = userMessage.Embeds.FirstOrDefault()?.Image?.Url;
            state.OriginalMessageId = targetMessage.Id;
            state.OriginalChannelId = targetMessage.Channel.Id;
            state.GiveawayId = giveaway.Id;
            state.EndTimeUtc = giveaway.EndTime;
            state.DurationMinutes = 0;

            ModalBuilder modal = new ModalBuilder()
                .WithCustomId("giveaway_modal")
                .WithTitle("Edit Giveaway")
                .AddTextInput("What's the prize", "prize", TextInputStyle.Short, placeholder: "The key to my heart", value: state.ModalData.Prize, required: true)
                .AddTextInput("How many can win", "winners", TextInputStyle.Short, value: state.ModalData.Winners, required: true)
                .AddTextInput("Reaction ReactionEmoji", "reaction_emoji", TextInputStyle.Short, value: state.ModalData.ReactionEmoji, required: true)
                .AddFileUpload("Image (optional)", "image", 0, 1, false);

            await RespondWithModalAsync(modal.Build());
        }
    }
}
