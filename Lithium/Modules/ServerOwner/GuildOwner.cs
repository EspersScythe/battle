using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Lithium.Discord.Contexts;
using Lithium.Discord.Preconditions;
using Lithium.Handlers;

namespace Lithium.Modules.ServerOwner
{
    [RequireOwner.ServerOwner]
    public class Serverowner : Base
    {
        [Command("setprefix")]
        [Summary("setprefix <prefix>")]
        [Remarks("set a custom prefix for the bot")]
        public async Task Prefix([Remainder] string newprefix = null)
        {
            if (newprefix.StartsWith("(") && newprefix.EndsWith(")"))
            {
                newprefix = newprefix.Remove(newprefix.Length - 1, 1).Remove(0, 1);
            }

            Context.Server.Settings.Prefix = newprefix;
            Context.Server.Save();
            await ReplyAsync($"Success, new prefix is: `{newprefix}`");
        }

        [Command("addmod")]
        [Summary("addmod <@role>")]
        [Remarks("Add a new moderator role")]
        public async Task AddModRole(IRole ModRole = null)
        {
            if (ModRole == null)
            {
                await ReplyAsync("Please provide a role to add");
                return;
            }

            if (!Context.Server.ModerationSetup.ModeratorRoles.Contains(ModRole.Id))
            {
                Context.Server.ModerationSetup.ModeratorRoles.Add(ModRole.Id);
                Context.Server.Save();
            }

            await ReplyAsync("Moderator Role added.");
        }

        [Command("delmod")]
        [Summary("delmod <@role>")]
        [Remarks("Delete a moderator role")]
        public async Task DelModRole(IRole ModRole = null)
        {
            if (ModRole == null)
            {
                await ReplyAsync("Please provide a role to add");
                return;
            }

            if (Context.Server.ModerationSetup.ModeratorRoles.Contains(ModRole.Id))
            {
                Context.Server.ModerationSetup.ModeratorRoles.Remove(ModRole.Id);
                Context.Server.Save();
                await ReplyAsync("Moderator Role Removed.");
            }
            else
            {
                await ReplyAsync("That is not a moderator role.");
            }
        }

        [Command("addadmin")]
        [Summary("addadmin <@role>")]
        [Remarks("Add a new administrator role")]
        public async Task AddAdminRole(IRole AdminRole = null)
        {
            if (AdminRole == null)
            {
                await ReplyAsync("Please provide a role to add");
                return;
            }

            if (!Context.Server.ModerationSetup.AdminRoles.Contains(AdminRole.Id))
            {
                Context.Server.ModerationSetup.AdminRoles.Add(AdminRole.Id);
                Context.Server.Save();
            }

            await ReplyAsync("Admin Role added.");
        }

        [Command("deladmin")]
        [Summary("deladmin <@role>")]
        [Remarks("Delete an admin role")]
        public async Task DelAdmin(IRole AdminRole = null)
        {
            if (AdminRole == null)
            {
                await ReplyAsync("Please provide a role to add");
                return;
            }

            if (Context.Server.ModerationSetup.AdminRoles.Contains(AdminRole.Id))
            {
                Context.Server.ModerationSetup.AdminRoles.Remove(AdminRole.Id);
                Context.Server.Save();
                await ReplyAsync("Admin Role Removed.");
            }
            else
            {
                await ReplyAsync("That is not a admin role.");
            }
        }

        [Command("reset")]
        [Summary("reset <confirm>")]
        [Remarks("reset the entire guild's config")]
        public async Task AddAdminRole(string confirmcode = null)
        {
            if (confirmcode != "287f3njg3o5")
            {
                await ReplyAsync("Please use this command again using the confirm code: `287f3njg3o5` to rset the server config. NOTE: This is permanent and cannot be reversed");
                return;
            }

            DatabaseHandler.RemoveGuild(Context.Guild.Id);
            DatabaseHandler.AddGuild(Context.Guild.Id);

            await ReplyAsync("Config Reset.");
        }
    }
}