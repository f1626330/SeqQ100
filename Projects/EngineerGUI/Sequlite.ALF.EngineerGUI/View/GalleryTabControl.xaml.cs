using Sequlite.ALF.EngineerGUI.ViewModel;
using System.Windows.Controls;
using System.Windows.Input;

namespace Sequlite.ALF.EngineerGUI.View
{
    /// <summary>
    /// GalleryTabControl.xaml 的交互逻辑
    /// </summary>
    public partial class GalleryTabControl : UserControl
    {
        public GalleryTabControl()
        {
            InitializeComponent();
        }

        private void _BlackBox_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ImageGalleryViewModel vm = DataContext as ImageGalleryViewModel;
                if (vm != null)
                {
                    vm.ActiveFile.BlackValue = int.Parse((sender as TextBox).Text);
                    vm.SliderManualContrastCommand.Execute(null);
                }
            }
        }

        private void _WhiteBox_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ImageGalleryViewModel vm = DataContext as ImageGalleryViewModel;
                if (vm != null)
                {
                    vm.ActiveFile.WhiteValue = int.Parse((sender as TextBox).Text);
                    vm.SliderManualContrastCommand.Execute(null);
                }
            }
        }
    }
}
