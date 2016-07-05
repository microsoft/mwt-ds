using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace DecisionServicePrivateWeb.Models
{
    public class SimulationViewModel
    {
        public string Password { get; set; }

        public string WebServiceToken { get; set; }

        public string TrainerToken { get; set; }

        public EvaluationViewModel EvaluationView { get; set; }

        public string TrainerArguments { get; set; }
    }
}