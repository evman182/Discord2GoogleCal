using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;


namespace Discord2GoogleCal
{
    public class CalendarApiClient
    {
        private readonly string _calendarId;
        private readonly string _appName;
        private readonly CalendarService _service;

        static string[] Scopes = { CalendarService.Scope.Calendar };

        public CalendarApiClient(string calendarId, string clientSecretPath, string credentialsPath, string applicationName)
        {
            _calendarId = calendarId;
            _appName = applicationName;
            var credential = GetApiCredential(clientSecretPath, credentialsPath);
            _service = GetCalendarService(credential);
        }

        // Grabs next 35 days of events and deletes them. Then reloads events.
        public void LoadDiscordEventsToCalendar(List<Event> events)
        {
            var existingEvents = GetEventsForUpcomingDays(7 * 5);
            DeleteEvents(existingEvents);
            UploadGCalEvents(events);
        }

        private void UploadGCalEvents(List<Event> gcalEvents)
        {
            foreach (var gcalEvent in gcalEvents)
            {
                var insertRequest = _service.Events.Insert(gcalEvent, _calendarId);
                insertRequest.Execute();
            }
        }

        // Deletes events but leaves weekly happy hour event as that doesn't come from discord 
        // and we want to leave it
        private void DeleteEvents(Events events)
        {
            foreach (var eventItem in events.Items)
            {
                if (eventItem.Summary.Contains("Weekly Happy Hour"))
                    continue;

                var deleteRequest = _service.Events.Delete(_calendarId, eventItem.Id);
                deleteRequest.Execute();
            }
        }

        private Events GetEventsForUpcomingDays(int numberOfDays)
        {
            var request = _service.Events.List(_calendarId);
            var today = TimeZoneInfo.ConvertTime(DateTime.Now, TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time")).Date;
            request.TimeMin = today;
            request.TimeMax = today.AddDays(numberOfDays);
            request.ShowDeleted = false;
            request.SingleEvents = true;
            request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;

            var events = request.Execute();
            return events;
        }

        private CalendarService GetCalendarService(UserCredential credential)
        {
            var service = new CalendarService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = _appName,
            });
            return service;
        }

        private static UserCredential GetApiCredential(string clientSecretPath, string credentialsPath)
        {
            UserCredential credential;
            using (var stream = new FileStream(clientSecretPath, FileMode.Open, FileAccess.Read))
            {
                credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.Load(stream).Secrets,
                    Scopes,
                    "user",
                    CancellationToken.None,
                    new FileDataStore(credentialsPath, true)).Result;
            }
            return credential;
        }
    }
}