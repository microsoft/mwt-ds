using System;
using System.Net;

namespace DecisionServicePrivateWeb.Classes
{
    public class TimeoutWebClient : WebClient
    {
        protected override WebRequest GetWebRequest(Uri uri)
        {
            WebRequest w = base.GetWebRequest(uri);
            w.Timeout = (int)TimeSpan.FromSeconds(2).TotalMilliseconds;
            return w;
        }
    }
}