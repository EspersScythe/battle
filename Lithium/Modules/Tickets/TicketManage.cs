using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Lithium.Discord.Contexts;
using Lithium.Discord.Preconditions;

namespace Lithium.Modules.Tickets
{
    [RequireContext(ContextType.Guild)]
    [Group("TicketManage")]
    public class TicketManage : Base
    {
        [RequireRole.RequireAdmin]
        [Command("Toggle")]
        [Summary("TicketManage Toggle")]
        [Remarks("Toggle the ticketing system")]
        public async Task ToggleTicketing()
        {
            Context.Server.Tickets.Settings.useticketing = !Context.Server.Tickets.Settings.useticketing;
            Context.Server.Save();
            await ReplyAsync($"Use Ticketing System: {Context.Server.Tickets.Settings.useticketing}");
        }

        [RequireRole.RequireAdmin]
        [Command("SetChannel")]
        [Summary("TicketManage SetChannel")]
        [Remarks("Set the ticketing channel")]
        public async Task SetChannel()
        {
            Context.Server.Tickets.Settings.ticketchannelid = Context.Channel.Id;
            Context.Server.Save();
            await ReplyAsync($"Ticket updates will now be logged in {Context.Channel.Name}");
        }

        [RequireRole.RequireAdmin]
        [Command("ToggleAllowAll")]
        [Summary("TicketManage ToggleAllowAll")]
        [Remarks("Toggle the ability to let any user create a ticket.")]
        public async Task ToggleAllow()
        {
            Context.Server.Tickets.Settings.allowAnyUserToCreate = !Context.Server.Tickets.Settings.allowAnyUserToCreate;
            Context.Server.Save();
            await ReplyAsync($"Allow any user in the server to create tickets: {Context.Server.Tickets.Settings.allowAnyUserToCreate}");
        }

        [RequireRole.RequireAdmin]
        [Command("AllowRole Add")]
        [Summary("TicketManage AllowRole Add <@role>")]
        [Remarks("add role to the ticketing creation allowed list")]
        public async Task AddAllow(IRole AllowRole = null)
        {
            if (AllowRole == null)
            {
                await ReplyAsync("Please provide a role.");
                return;
            }

            Context.Server.Tickets.Settings.AllowedCreationRoles.Add(AllowRole.Id);
            Context.Server.Save();
            await ReplyAsync($"Allowed Roles:\n" +
                             $"{string.Join("\n", Context.Server.Tickets.Settings.AllowedCreationRoles.Select(x => Context.Guild.GetRole(x)).Where(x => x != null).Select(x => x.Name))}");
        }

        [RequireRole.RequireAdmin]
        [Command("AllowRole Remove")]
        [Summary("TicketManage AllowRole Remove <@role>")]
        [Remarks("Remove role from the ticketing creation allowed list")]
        public async Task RemoveAllow(IRole AllowRole = null)
        {
            if (AllowRole == null)
            {
                await ReplyAsync("Please provide a role.");
                return;
            }

            Context.Server.Tickets.Settings.AllowedCreationRoles.Remove(AllowRole.Id);
            Context.Server.Save();
            await ReplyAsync($"Allowed Roles:\n" +
                             $"{string.Join("\n", Context.Server.Tickets.Settings.AllowedCreationRoles.Select(x => Context.Guild.GetRole(x)).Where(x => x != null).Select(x => x.Name))}");
        }

        [RequireRole.RequireModerator]
        [Command("ToggleSolved")]
        [Summary("TicketManage ToggleSolved <ID> [Optional]<reason>")]
        [Remarks("Toggle the solved status of a ticket")]
        public async Task ToggleSolved(int id, [Remainder] string reason = null)
        {
            var targetticket = Context.Server.Tickets.tickets.FirstOrDefault(x => x.id == id);
            if (targetticket == null)
            {
                await ReplyAsync("There is no ticket with that ID.");
                return;
            }

            targetticket.solved = !targetticket.solved;
            targetticket.solvedmessage = reason;
            Context.Server.Save();

            var emb = new EmbedBuilder
            {
                Title = $"Solved: {targetticket.solved}",
                Description = $"Ticket By: {Context.Socket.Guild.GetUser(targetticket.InitUser)?.Username ?? $"Missing User [{targetticket.InitUser}]"}\n" +
                              $"Message: {targetticket.message}\n\n" +
                              $"^ [{targetticket.Up.Count}] v [{targetticket.Down.Count}]\n" +
                              $"ID: {targetticket.id}",
                Footer = new EmbedFooterBuilder
                {
                    Text = $"Ran by {Context.User.Username}"
                },
                Color = targetticket.solved ? Color.Green : Color.Red
            };
            await SendEmbedAsync(emb);
            await Context.Server.TicketLog(emb, Context.Guild);
        }
    }
}