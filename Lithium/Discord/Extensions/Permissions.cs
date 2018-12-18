using System.Linq;
using Discord;
using Discord.WebSocket;
using Lithium.Handlers;

namespace Lithium.Discord.Extensions
{
    public class Permissions
    {
        /// <summary>
        ///     Returns true if the targetuser has a higher position (rank) in the guild than the client (bot)
        /// </summary>
        /// <param name="targetuser"></param>
        /// <param name="client"></param>
        /// <returns></returns>
        public static bool CheckHeirachy(IGuildUser targetuser, IDiscordClient client)
        {
            var guild = targetuser.Guild as SocketGuild;
            var userrole = guild.Roles.OrderByDescending(x => x.Position).FirstOrDefault(x => targetuser.RoleIds.Contains(x.Id));
            if (userrole == null)
            {
                return false;
            }

            var gclient = guild.Users.First(x => x.Id == client.CurrentUser.Id);
            //Here we could also check if the bot has roles, however it should always have a role as upon joining the bot user will have a managed permissions role
            return userrole.Position > gclient.Roles.Max(x => x.Position);
        }

        /// <summary>
        ///     Returns true if targetuser is a moderator in the server
        /// </summary>
        /// <param name="targetuser">The user to check</param>
        /// <param name="AllowAdminRole">Optionally allow users with roles set by the SetAdmin Command</param>
        /// <param name="AllowAdminPermission">Optionally allow users with the guild admin permission</param>
        /// <returns></returns>
        public static bool IsModerator(IGuildUser targetuser, bool AllowAdminRole = true, bool AllowAdminPermission = true)
        {
            var guild = DatabaseHandler.GetGuild(targetuser.GuildId);
            if (targetuser.Guild.OwnerId == targetuser.Id)
            {
                return true;
            }

            if (targetuser.RoleIds.Any(x => guild.ModerationSetup.ModeratorRoles.Contains(x)))
            {
                return true;
            }

            if (targetuser.RoleIds.Any(x => guild.ModerationSetup.AdminRoles.Contains(x)) && AllowAdminRole)
            {
                return true;
            }

            if (targetuser.GuildPermissions.Administrator && AllowAdminPermission)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        ///     Returns true if targetuser is an admin in the server (via the SetAdmin command)
        /// </summary>
        /// <param name="targetuser">The user to check</param>
        /// <param name="AllowAdminPermission">
        ///     Optionally return true for users with a role containing the Server Administrator
        ///     permission
        /// </param>
        /// <returns></returns>
        public static bool IsAdmin(IGuildUser targetuser, bool AllowAdminPermission = true)
        {
            var guild = DatabaseHandler.GetGuild(targetuser.GuildId);
            if (targetuser.Guild.OwnerId == targetuser.Id)
            {
                return true;
            }

            if (targetuser.RoleIds.Any(x => guild.ModerationSetup.AdminRoles.Contains(x)))
            {
                return true;
            }

            if (targetuser.GuildPermissions.Administrator && AllowAdminPermission)
            {
                return true;
            }

            return false;
        }
    }
}