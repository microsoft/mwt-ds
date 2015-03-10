using ClientDecisionServiceWebSample.Hubs;
using Microsoft.AspNet.SignalR;
using System.Diagnostics;

namespace ClientDecisionServiceWebSample.Extensions
{
    public class SignalRTraceListener : TraceListener
    {
        public override void Write(string message)
        {
        }

        public override void WriteLine(string message)
        {
            IHubContext hub = GlobalHost.ConnectionManager.GetHubContext<TraceHub>();
            hub.Clients.All.addNewMessageToPage(message);
        }
    }
}