using Sequlite.ALF.EngineerGUI.ViewModel;
using System.Windows.Controls;
using System.Windows.Input;

namespace Sequlite.ALF.EngineerGUI.View
{
    /// <summary>
    /// ChemiTemperControl.xaml 的交互逻辑
    /// </summary>
    public partial class ChemiTemperControl : UserControl
    {
        public ChemiTemperControl()
        {
            InitializeComponent();
        }

        private void _IntervalBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ChemistryViewModel vm = DataContext as ChemistryViewModel;
                if (vm != null)
                {
                    int interval = 0;
                    if (int.TryParse(_IntervalBox.Text, out interval))
                    {
                        vm.SampleInterval = interval;
                    }
                }
            }
        }
    }
}
