using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace DecisionServicePrivateWeb.Classes
{
    public class EvalD3
    {
        public string key { get; set; }

        public Dictionary<DateTime, float> values { get; set; }
    }
}