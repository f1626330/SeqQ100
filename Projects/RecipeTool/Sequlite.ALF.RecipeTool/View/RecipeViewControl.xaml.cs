using Sequlite.ALF.RecipeTool.ViewModel;
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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Sequlite.ALF.RecipeTool.View
{
    /// <summary>
    /// RecipeViewControl.xaml 的交互逻辑
    /// </summary>
    public partial class RecipeViewControl : UserControl
    {
        public RecipeViewControl()
        {
            InitializeComponent();
        }

        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            RecipeToolRecipeViewModel vm = DataContext as RecipeToolRecipeViewModel;
            if (vm != null)
            {
                vm.SelectedStep = (StepsTreeViewModel)((TreeView)sender).SelectedItem;
            }
        }
    }
}
