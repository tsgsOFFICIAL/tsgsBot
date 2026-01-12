using Discord.Interactions;
using Discord.WebSocket;
using Discord;

namespace tsgsBot_C_.Commands.Public
{
    public sealed class TicTacToeCommand : LoggedCommandModule
    {
        [SlashCommand("tictactoe", "Challenge someone to Tic-Tac-Toe")]
        [CommandContextType(InteractionContextType.Guild)]
        [DefaultMemberPermissions(GuildPermission.UseApplicationCommands)]
        public async Task TicTacToeAsync(IUser opponent)
        {
            await LogCommandAsync();

            SocketUser p1 = Context.User;
            IUser p2 = opponent;

            if (p2.Id == p1.Id)
            {
                await RespondAsync("You can't play yourself.", ephemeral: true);
                return;
            }

            if (p2.IsBot)
            {
                await RespondAsync("Bots are trash at Tic-Tac-Toe.", ephemeral: true);
                return;
            }

            string board = "_________"; // 9 empty cells
            MessageComponent components = TicTacToeComponents.Build(p1.Id, p2.Id, 1, board);

            await RespondAsync(
                embed: new EmbedBuilder()
                    .WithTitle("Tic-Tac-Toe")
                    .WithDescription(
                        $"{p1.Mention} ❌ vs {p2.Mention} ⭕\n\n" +
                        $"**Turn:** {p1.Mention}"
                    )
                    .WithColor(Color.Green)
                    .Build(),
                components: components
            );
        }
    }

    public sealed class TicTacToeButtons : InteractionModuleBase<SocketInteractionContext>
    {
        [ComponentInteraction("ttt:*")]
        public async Task HandleMove(string data)
        {
            await DeferAsync();

            // data = "<p1>:<p2>:<turn>:<board>:<index>"
            string[] parts = data.Split(':');

            ulong p1 = ulong.Parse(parts[0]);
            ulong p2 = ulong.Parse(parts[1]);
            int turn = int.Parse(parts[2]);
            char[] board = parts[3].ToCharArray();
            int index = int.Parse(parts[4]);

            ulong currentPlayer = turn == 1 ? p1 : p2;

            if (Context.User.Id != currentPlayer)
            {
                await FollowupAsync("It's not your turn.", ephemeral: true);
                return;
            }

            if (board[index] != '_')
            {
                await FollowupAsync("That spot is already taken.", ephemeral: true);
                return;
            }

            board[index] = turn == 1 ? 'X' : 'O';

            if (TicTacToeLogic.CheckWin(board))
            {
                await ModifyOriginalResponseAsync(msg =>
                {
                    msg.Embed = new EmbedBuilder()
                        .WithTitle("🎉 Game Over")
                        .WithDescription($"<@{currentPlayer}> **wins!**")
                        .WithColor(Color.Gold)
                        .Build();

                    msg.Components = new ComponentBuilder().Build();
                });

                return;
            }

            if (!board.Contains('_'))
            {
                await ModifyOriginalResponseAsync(msg =>
                {
                    msg.Embed = new EmbedBuilder()
                        .WithTitle("🤝 Game Over")
                        .WithDescription("It's a **draw!**")
                        .WithColor(Color.LightGrey)
                        .Build();

                    msg.Components = new ComponentBuilder().Build();
                });

                return;
            }

            int nextTurn = turn == 1 ? 2 : 1;

            await ModifyOriginalResponseAsync(msg =>
            {
                msg.Embed = new EmbedBuilder()
                    .WithTitle("Tic-Tac-Toe")
                    .WithDescription($"**Turn:** <@{(nextTurn == 1 ? p1 : p2)}>")
                    .WithColor(Color.Green)
                    .Build();

                msg.Components = TicTacToeComponents.Build(p1, p2, nextTurn, new string(board));
            });

        }
    }

    public static class TicTacToeComponents
    {
        public static MessageComponent Build(ulong p1, ulong p2, int turn, string board)
        {
            ComponentBuilder builder = new ComponentBuilder();

            for (int i = 0; i < 9; i++)
            {
                int row = i / 3;

                string label;
                ButtonStyle style;
                bool disabled = board[i] != '_';

                switch (board[i])
                {
                    case 'X':
                        label = "❌";
                        style = ButtonStyle.Primary;
                        break;

                    case 'O':
                        label = "⭕";
                        style = ButtonStyle.Danger;
                        break;

                    default:
                        label = "⬜";
                        style = ButtonStyle.Secondary;
                        break;
                }

                builder.WithButton(
                    label: label,
                    customId: $"ttt:{p1}:{p2}:{turn}:{board}:{i}",
                    style: style,
                    disabled: disabled,
                    row: row
                );
            }

            return builder.Build();
        }
    }

    public static class TicTacToeLogic
    {
        private static readonly int[][] Wins =
        {
        [0,1,2], [3,4,5], [6,7,8],
        [0,3,6], [1,4,7], [2,5,8],
        [0,4,8], [2,4,6]
    };

        public static bool CheckWin(char[] board)
        {
            foreach (int[] line in Wins)
            {
                if (board[line[0]] != '_' &&
                    board[line[0]] == board[line[1]] &&
                    board[line[1]] == board[line[2]])
                    return true;
            }
            return false;
        }
    }
}