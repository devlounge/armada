using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace GResearch.Armada.Client
{
    public interface IEvent
    {
        string JobId { get; }
        string JobSetId { get;  }
        string Queue { get;  }
        System.DateTimeOffset? Created { get; }
    }
    
    public interface IArmadaClient
    {
        Task<ApiCancellationResult> CancelJobsAsync(ApiJobCancelRequest body);
        Task<ApiJobSubmitResponse> SubmitJobsAsync(ApiJobSubmitRequest body);
        Task<object> CreateQueueAsync(string name, ApiQueue body);
        Task<IEnumerable<StreamResponse<ApiEventStreamMessage>>> GetJobEventsStream(string jobSetId, string fromMessage = null, bool watch = false);
        Task WatchEvents(string jobSetId,
            string fromMessageId, 
            CancellationToken ct,
            Action<StreamResponse<ApiEventStreamMessage>> onMessage, 
            Action<Exception> onException = null);
    }

    public partial class ApiEventMessage
    {
        public IEvent Event => Cancelled ?? Submitted ?? Queued ?? Leased ?? LeaseReturned ??
                               LeaseExpired ?? Pending ?? Running ?? UnableToSchedule ??
                               Failed ?? Succeeded ?? Reprioritized ?? Cancelling ?? Cancelled ?? Terminated as IEvent;
    }

    public partial class ApiJobSubmittedEvent : IEvent {}
    public partial class ApiJobQueuedEvent : IEvent {}
    public partial class ApiJobLeasedEvent : IEvent {}
    public partial class ApiJobLeaseReturnedEvent : IEvent {}
    public partial class ApiJobLeaseExpiredEvent : IEvent {}
    public partial class ApiJobPendingEvent : IEvent {}
    public partial class ApiJobRunningEvent : IEvent {}
    public partial class ApiJobUnableToScheduleEvent : IEvent {}
    public partial class ApiJobFailedEvent : IEvent {}
    public partial class ApiJobSucceededEvent : IEvent {}
    public partial class ApiJobReprioritizedEvent  : IEvent {}
    public partial class ApiJobCancellingEvent  : IEvent {}
    public partial class ApiJobCancelledEvent  : IEvent {}
    public partial class ApiJobTerminatedEvent : IEvent {}

    public class StreamResponse<T>
    {
        public T Result { get; set; }
        public string Error { get; set; }
    }

    public partial class ArmadaClient : IArmadaClient
    {
        public async Task<IEnumerable<StreamResponse<ApiEventStreamMessage>>> GetJobEventsStream(string jobSetId,
            string fromMessageId = null, bool watch = false)
        {
            var fileResponse = await GetJobSetEventsCoreAsync(jobSetId,
                new ApiJobSetRequest {FromMessageId = fromMessageId, Watch = watch});
            return ReadEventStream(fileResponse.Stream);
        }
        
        private IEnumerable<StreamResponse<ApiEventStreamMessage>> ReadEventStream(Stream stream)
        {
            using (var reader = new StreamReader(stream))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    var eventMessage =
                        JsonConvert.DeserializeObject<StreamResponse<ApiEventStreamMessage>>(line,
                            this.JsonSerializerSettings);
                    yield return eventMessage;
                }
            }
        }

        public async Task WatchEvents(
            string jobSetId, 
            string fromMessageId, 
            CancellationToken ct, 
            Action<StreamResponse<ApiEventStreamMessage>> onMessage,
            Action<Exception> onException = null)
        {
            var failCount = 0;
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    using (var fileResponse = await GetJobSetEventsCoreAsync(jobSetId,
                        new ApiJobSetRequest {FromMessageId = fromMessageId, Watch = true}, ct))
                    using (var reader = new StreamReader(fileResponse.Stream))
                    {
                        try
                        {
                            failCount = 0;
                            while (!ct.IsCancellationRequested && !reader.EndOfStream)
                            {
                                var line = await reader.ReadLineAsync();
                                var eventMessage =
                                    JsonConvert.DeserializeObject<StreamResponse<ApiEventStreamMessage>>(line,
                                        this.JsonSerializerSettings);

                                onMessage(eventMessage);
                                fromMessageId = eventMessage.Result?.Id ?? fromMessageId;
                            }
                        }
                        catch (IOException)
                        {
                            // Stream was probably closed by the server, continue to reconnect
                        }
                    }
                }
                catch (TaskCanceledException)
                {
                    // Server closed the connection, continue to reconnect
                }
                catch (Exception e)
                {
                    failCount++;
                    onException?.Invoke(e);
                    // gradually back off
                    await Task.Delay(TimeSpan.FromSeconds(Math.Min(300, Math.Pow(2 ,failCount))), ct);
                }
            }
        }
    }
}