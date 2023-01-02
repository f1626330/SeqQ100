using Sequlite.ALF.EngineerGUI.ViewModel.RecipetoolVM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Sequlite.ALF.EngineerGUI.View.RecipetoolView
{
    /// <summary>
    /// Interaction logic for StepEditWindow.xaml
    /// </summary>
    public partial class StepEditWindow : Window
    {
        public StepEditWindow()
        {
            InitializeComponent();
            Loaded += StepEditWindow_Loaded;
        }

        private void StepEditWindow_Loaded(object sender, RoutedEventArgs e)
        {
            StepEditViewModel vm = DataContext as StepEditViewModel;
            if (vm != null)
            {
                vm.OnClosingWindow += Vm_OnClosingWindow;
            }
        }

        private void Vm_OnClosingWindow(bool updated)
        {
            this.DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = null;
            Close();
        }
    }
}
