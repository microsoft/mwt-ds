using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace DecisionServicePrivateWeb.Models
{
    public class EvaluationViewModel
    {
        public List<string> WindowFilters { get; set; }
        public string SelectedFilter { get; set; }
    }
}