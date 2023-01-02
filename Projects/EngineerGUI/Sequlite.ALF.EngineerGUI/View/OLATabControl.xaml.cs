using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using InteractiveDataDisplay.WPF;
using Sequlite.ALF.EngineerGUI.ViewModel;

namespace Sequlite.ALF.EngineerGUI.View
{
    /// <summary>
    /// Interaction logic for RIPPTabControl.xaml
    /// </summary>
    public partial class OLATabControl : UserControl
    {
       
        public OLATabControl()
        {
            InitializeComponent();
            
            this.Loaded += OLATabControl_Loaded;
            
        }

        private void OLATabControl_Loaded(object sender, RoutedEventArgs e)
        {
            object d = this.DataContext;
            if (d is RecipeViewModel)
            {
                RecipeViewModel recipeVM = ((RecipeViewModel)d);
                recipeVM.SetGraph(linegraph);
            }

        }
    }
}
