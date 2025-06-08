using System.Collections.ObjectModel;

namespace StackSuite.ViewModels
{
    public class SessionGroupViewModel
    {
        public string GroupName { get; set; } = "";
        public ObservableCollection<SessionBaseViewModel> Sessions { get; set; } = new();
    }
}