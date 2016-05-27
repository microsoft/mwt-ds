using Microsoft.Research.MultiWorldTesting.Contract;

namespace DecisionServicePrivateWeb.Models
{
    public class SettingsSaveModel
    {
        public string AdditionalTrainArguments { get; set; }
        public string SelectedModelId { get; set; }
        public bool IsExplorationEnabled { get; set; }
    }
}