using System.Linq;
using Discord;
using Lithium.Models;

namespace Lithium.Discord.Extensions
{
    public class TicketAvailable
    {
        /// <summary>
        ///     Returns true if the given user is allowed to create tickets
        /// </summary>
        /// <param name="Settings"></param>
        /// <param name="User"></param>
        /// <returns></returns>
        public static bool CanCreate(GuildModel.Guild.ticketing.tsettings Settings, IGuildUser User)
        {
            if (Settings.allowAnyUserToCreate)
            {
                return true;
            }

            if (User.RoleIds.Any(x => Settings.AllowedCreationRoles.Contains(x)))
            {
                return true;
            }

            return false;
        }
    }
}