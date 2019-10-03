using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Discord;
using Discord.Rest;
using Google.Apis.Calendar.v3.Data;

namespace Discord2GoogleCal
{
    public class DiscordCalendarGenerator
    {

        private readonly DiscordRestClient _client = new DiscordRestClient();
        private readonly RestGuild _guild;
        private readonly RestTextChannel _channel;
        private readonly ulong _adminUserId;
        private readonly HashSet<ulong> _messageIdsToIgnore;

        public DiscordCalendarGenerator(
            string discordBotToken, 
            ulong guildId, 
            ulong channelId, 
            ulong adminUserId,
            HashSet<ulong> messageIdsToIgnore)
        {
            _client.LoginAsync(TokenType.Bot, discordBotToken).Wait();
            _guild = _client.GetGuildAsync(guildId).Result;
            _channel = _guild.GetTextChannelAsync(channelId).Result;
            _adminUserId = adminUserId;
            _messageIdsToIgnore = messageIdsToIgnore;
        }

        // Gets all messages in channel (which is prob dumb) and then gets events within the next 35 days.
        // Hardcoded to Eastern because that's where the events always took place. Should prob be configurable.
        public List<Event> GetEvents()
        {
            var messages = GetDiscordMessages();
            var today = TimeZoneInfo.ConvertTime(DateTime.Now, TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time")).Date;
            var endDate = today.AddDays(5 * 7);
            var events = GetEventsFromMessagesWithinDateRange(messages, today, endDate);
            return events;
        }

        private IEnumerable<RestMessage> GetDiscordMessages()
        {
            var messages = _channel.GetMessagesAsync().FlattenAsync().Result;
            return messages;
        }

        private List<Event> GetEventsFromMessagesWithinDateRange(IEnumerable<RestMessage> messages, DateTime startDate, DateTime endDate)
        {
            var events = new List<Event>();
            foreach (var message in messages.Where(m => !_messageIdsToIgnore.Contains(m.Id)))
            {
                var body = message.Content;
                try
                {
                    var fixedBody = SwapIdsForText(body);
                    var title = body.Substring(0, body.IndexOf('\n')).Trim('*');
                    var date = GetDateFromMessageBody(body);
                    if (date >= startDate && date <= endDate)
                    {
                        events.Add(new Event
                        {
                            Summary = title,
                            Description = fixedBody,
                            Start = new EventDateTime { Date = date.ToString("yyyy-MM-dd") },
                            End = new EventDateTime { Date = date.ToString("yyyy-MM-dd") },
                        });
                    }
                }
                catch (Exception ex)
                {
                    var dmWithAdmin = _client.GetUserAsync(_adminUserId).Result.GetOrCreateDMChannelAsync().Result;
                    var messageContent = $"Error processing event:\n{body}\n\nException: {ex}";
                    var messageToSend = messageContent.Length > 2000 ? messageContent.Substring(0, 2000) : messageContent;
                    dmWithAdmin.SendMessageAsync(messageToSend).Wait();
                }
            }

            return events;
        }

        // This swaps out any user or channel ids in the raw message text for the actual text value
        private string SwapIdsForText(string body)
        {
            string newString = body;
            foreach (Match match in Regex.Matches(body, "[<]@[!]?([0-9]+)[>]"))
            {
                var matchedId = match.Groups[1].Value;
                var parsedMatchedId = ulong.Parse(matchedId);
                var user = _client.GetGuildUserAsync(_guild.Id, parsedMatchedId).Result;
                var userName = user.Nickname ?? user.Username;
                newString = newString.Replace(match.Value, "@" + userName);
            }

            foreach (Match match in Regex.Matches(body, "[<]#[0-9]+[>]"))
            {
                var matchedId = match.Value.Substring(2, match.Value.Length - 3);
                var parsedMatchedId = ulong.Parse(matchedId);
                var channel = _guild.GetTextChannelAsync(parsedMatchedId).Result;
                var channelName = channel.Name;
                newString = newString.Replace(match.Value, "#" + channelName);
            }

            return newString;
        }

        private static DateTime GetDateFromMessageBody(string body)
        {
            // Breakdown of the regex:
            // The asterisks at the beginning are to cover if the Date section is bolded.
            // We then account for spaces, and then accept a m/d/y formatted date, 
            // accepting 1 or 2 digit month/days and a 2 or 4 digit year. This can easily be improved with better regex
            // or a date parser
            var dateMatch = Regex.Match(body, @".*Date:([*][*])?[\s]*([0-9]{1,2}/[0-9]{1,2}/(20)?[0-9]{2}).*");
            var dateString = dateMatch.Groups[2].Value;
            var date = DateTime.Parse(dateString);
            return date;
        }
    }
}