using tsgsBot_C_.StateServices;
using Discord.Interactions;
using tsgsBot_C_.Models;
using Discord;

namespace tsgsBot_C_.Commands.Public;

public sealed class SupportCommand(SupportFormStateService stateService) : LoggedCommandModule
{
    [SlashCommand("support", "Submit a support request for one of my applications")]
    [CommandContextType(InteractionContextType.Guild)]
    [DefaultMemberPermissions(GuildPermission.UseApplicationCommands)]
    public async Task SupportAsync()
    {
        stateService.Clear(Context.User.Id); // Clear any previous state for this user

        SelectMenuBuilder appMenu = new SelectMenuBuilder()
            .WithCustomId("support_app")
            .WithPlaceholder("Which application is this for?")
            .AddOption("CS2 AutoAccept", "cs2aa")
            .AddOption("Stream Drop Collector", "sdc");

        await RespondAsync("First select the **application**.",
            components: new ComponentBuilder().WithSelectMenu(appMenu).Build(),
            ephemeral: true);

        await LogCommandAsync();
    }

    // App selection - updates message with new components
    [ComponentInteraction("support_app")]
    public async Task AppSelected(string[] values)
    {
        await DeferAsync(ephemeral: true);

        if (values.Length == 0)
            return;

        UserSupportFormState state = stateService.GetOrCreate(Context.User.Id);
        state.SelectedApp = values[0];

        string appName = state.SelectedApp == "cs2aa" ? "CS2 AutoAccept" : "Stream Drop Collector";

        // Build dynamic menus (same as before)
        SelectMenuBuilder issueMenu = new SelectMenuBuilder().WithCustomId("support_issue_type").WithPlaceholder("Select Issue Type")
            .AddOption("🐞 Bug Report", "Bug Report").AddOption("💡 Feature Request", "Feature Request")
            .AddOption("❓ General Question", "General Question").AddOption("⚙️ Other", "Other");

        SelectMenuBuilder reproMenu = new SelectMenuBuilder().WithCustomId("support_repro").WithPlaceholder("Select Reproducibility")
            .AddOption("Every time", "Every time").AddOption("Occasionally", "Occasionally")
            .AddOption("Rarely", "Rarely").AddOption("Only once", "Only once");

        SelectMenuBuilder urgencyMenu = new SelectMenuBuilder().WithCustomId("support_urgency").WithPlaceholder("Select Urgency")
            .AddOption("Low", "Low").AddOption("Medium", "Medium").AddOption("High", "High").AddOption("Critical", "Critical");

        SelectMenuBuilder platformMenu = new SelectMenuBuilder().WithCustomId("support_platform").WithPlaceholder("Select Platform");

        if (state.SelectedApp == "cs2aa")
            platformMenu.AddOption("Faceit", "Faceit").AddOption("Matchmaking", "Regular Matchmaking").AddOption("Other", "Other");
        else
            platformMenu.AddOption("Twitch", "Twitch").AddOption("Kick", "Kick").AddOption("Both", "Both");

        ButtonBuilder continueBtn = new ButtonBuilder().WithLabel("Continue to Form").WithCustomId("support_continue").WithStyle(ButtonStyle.Primary);

        ComponentBuilder builder = new ComponentBuilder()
            .WithSelectMenu(issueMenu, row: 0)
            .WithSelectMenu(reproMenu, row: 1)
            .WithSelectMenu(urgencyMenu, row: 2)
            .WithSelectMenu(platformMenu, row: 3)
            .WithButton(continueBtn, row: 4);

        await Context.Interaction.ModifyOriginalResponseAsync(x =>
        {
            x.Content = $"Selected: **{appName}**\nNow fill out the remaining fields.";
            x.Components = builder.Build();
        });
    }

    // Generic handler for the other selects
    [ComponentInteraction("support_issue_type|support_repro|support_urgency|support_platform", TreatAsRegex = true)]
    public async Task HandleSelect(string[] values)
    {
        await DeferAsync(ephemeral: true);
        if (values.Length == 0)
            return;

        UserSupportFormState state = stateService.GetOrCreate(Context.User.Id);

        // Use the interaction's custom id from the component interaction context
        string? customId = (Context.Interaction as Discord.WebSocket.SocketMessageComponent)?.Data.CustomId;
        switch (customId)
        {
            case "support_issue_type": state.IssueType = values[0]; break;
            case "support_repro": state.Reproducibility = values[0]; break;
            case "support_urgency": state.Urgency = values[0]; break;
            case "support_platform": state.Platform = values[0]; break;
        }
    }

    // Continue button → show modal
    [ComponentInteraction("support_continue")]
    public async Task ContinueToForm()
    {
        if (!stateService.TryGet(Context.User.Id, out UserSupportFormState? state) || string.IsNullOrEmpty(state?.SelectedApp))
        {
            await RespondAsync("Please select an application first.", ephemeral: true);
            return;
        }

        string title = state.SelectedApp == "cs2aa" ? "CS2 AutoAccept — Support Form" : "Stream Drop Collector — Support Form";
        string versionLabel = state.SelectedApp == "cs2aa" ? "CS2-AutoAccept Version" : "Stream Drop Collector Version";

        ModalBuilder modal = new ModalBuilder()
            .WithTitle(title)
            .WithCustomId("support_modal_full")
            .AddTextInput("Describe the Issue", "description", TextInputStyle.Paragraph, required: true)
            .AddTextInput("Operating System", "os", TextInputStyle.Short, required: true)
            .AddTextInput(versionLabel, "version", TextInputStyle.Short, required: true)
            .AddTextInput("Steps to Reproduce", "steps", TextInputStyle.Paragraph, required: false)
            .AddTextInput("Additional Info / Logs", "additional", TextInputStyle.Paragraph, required: false);

        await Context.Interaction.RespondWithModalAsync(modal.Build());
    }

    // Modal handler - final step, sends embed + cleans up
    [ModalInteraction("support_modal_full")]
    public async Task ModalSubmitted(SupportModalModel modal)
    {
        await DeferAsync(ephemeral: true);

        if (!stateService.TryGet(Context.User.Id, out UserSupportFormState? state) || string.IsNullOrEmpty(state?.SelectedApp))
        {
            await FollowupAsync("Session expired or invalid. Please start over with /support", ephemeral: true);
            return;
        }

        string appName = state.SelectedApp == "cs2aa" ? "CS2 AutoAccept" : "Stream Drop Collector";

        EmbedBuilder embed = new EmbedBuilder()
            .WithTitle($"🧾 {appName} — Support Request")
            .WithColor(new Color(3, 169, 252))
            .WithCurrentTimestamp()
            .AddField("Application", appName, true)
            .AddField("Issue Type", state.IssueType ?? "Not specified", true)
            .AddField("Reproducibility", state.Reproducibility ?? "Not specified", true)
            .AddField("Urgency", state.Urgency ?? "Not specified", true)
            .AddField("Platform", state.Platform ?? "Not specified", true)
            .AddField("Operating System", modal.OS, true)
            .AddField("Version", modal.Version, true)
            .AddField("Description", modal.Description)
            .AddField("Steps", string.IsNullOrEmpty(modal.Steps) ? "Not provided" : modal.Steps)
            .AddField("Additional Info", string.IsNullOrEmpty(modal.Additional) ? "None" : modal.Additional);

        // Final cleanup - remove all components from original message
        await Context.Interaction.ModifyOriginalResponseAsync(msg =>
        {
            msg.Content = "✅ Support request submitted! You can close this.";
            msg.Embed = embed.Build();
            msg.Components = new ComponentBuilder().Build();  // ← removes ALL dropdowns/buttons
            msg.Flags = MessageFlags.Ephemeral;
        });

        // Send to support channel
        //if (ulong.TryParse("688841094980436069", out ulong chId)) // support
        if (ulong.TryParse("539104904845852683", out ulong chId)) // staff-testing
        {
            if (Context.Client.GetChannel(chId) is IMessageChannel channel)
                await channel.SendMessageAsync(embed: embed.Build());
        }

        // Clear state after success
        stateService.Clear(Context.User.Id);

        await LogCommandAsync(
            [
                ("Application",      state.SelectedApp == "cs2aa" ? "CS2 AutoAccept" : "Stream Drop Collector"),
                ("Issue Type",       state.IssueType    ?? "Not specified"),
                ("Reproducibility",  state.Reproducibility ?? "Not specified"),
                ("Urgency",          state.Urgency      ?? "Not specified"),
                ("Platform",         state.Platform     ?? "Not specified"),
                ("Operating System", modal.OS                  ?? "Unknown"),
                ("Version",          modal.Version                ?? "Not specified"),
                ("Description",      modal.Description            ?? "No description provided"),
                ("Steps",            string.IsNullOrWhiteSpace(modal.Steps) ? "Not provided" : modal.Steps.Trim()),
                ("Additional Info",  string.IsNullOrWhiteSpace(modal.Additional) ? "None" : modal.Additional.Trim())
            ]
        );
    }
}