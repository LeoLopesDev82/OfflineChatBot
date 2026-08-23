using OfflineChatBot.ViewModels;

namespace OfflineChatBot.Views
{
    public partial class ModelManagerWindow : ChromelessWindow
    {
        public ModelManagerWindow(ModelManagerViewModel viewModel)
        {
            InitializeComponent();

            DataContext = viewModel;
        }
    }
}