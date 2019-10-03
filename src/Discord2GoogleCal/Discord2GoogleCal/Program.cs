using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;

namespace Discord2GoogleCal
{
    class Program
    {
        private static readonly string DiscordGoogleCalendarId = ConfigurationManager.AppSettings["GoogleCalendarId"];
        private static readonly string GoogleClientSecretPath = ConfigurationManager.AppSettings["GoogleClientSecretPath"];
        private static readonly string GoogleCredentialsPath = ConfigurationManager.AppSettings["GoogleCredentialsPath"];
        private static readonly string ApplicationName = ConfigurationManager.AppSettings["ApplicationName"];

        private static readonly string DiscordBotToken = ConfigurationManager.AppSettings["DiscordBotToken"];
        private static readonly ulong GuildId = ulong.Parse(ConfigurationManager.AppSettings["GuildId"]);
        private static readonly ulong CalendarChannelId = ulong.Parse(ConfigurationManager.AppSettings["CalendarChannelId"]);
        private static readonly ulong AdminUserId = ulong.Parse(ConfigurationManager.AppSettings["AdminUserId"]);
        private static readonly HashSet<ulong> IgnoredMessages = GetMessageIdsToIgnore();

        static void Main(string[] args)
        {
            var discordCalendarGenerator = new DiscordCalendarGenerator(DiscordBotToken, GuildId, CalendarChannelId, AdminUserId, IgnoredMessages);
            var discordEvents = discordCalendarGenerator.GetEvents();
            var discordCalendarClient = new CalendarApiClient(DiscordGoogleCalendarId, GoogleClientSecretPath, GoogleCredentialsPath, ApplicationName);
            discordCalendarClient.LoadDiscordEventsToCalendar(discordEvents);
        }

        private static HashSet<ulong> GetMessageIdsToIgnore()
        {
            var rawMessageIds = ConfigurationManager.AppSettings["MessagesToIgnore"];
            return rawMessageIds.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(ulong.Parse).ToHashSet();
        }
    }
}
