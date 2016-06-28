using DecisionServicePrivateWeb.Validations;
using Microsoft.Research.MultiWorldTesting.Contract;

namespace DecisionServicePrivateWeb.Models
{
    public class SettingsSaveModel
    {
        public string TrainArguments { get; set; }

        public string SelectedModelId { get; set; }

        public bool IsExplorationEnabled { get; set; }

        [UnitInterval(ErrorMessage = "Initial Exploration Epsilon must be a value in the interval [0,1]")]
        public float InitialExplorationEpsilon { get; set; }
    }
}