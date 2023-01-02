using System.Windows;
using System.Windows.Controls;

namespace Sequlite.ALF.EngineerGUI.View
{
    /// <summary>
    /// RecipeViewControl.xaml 的交互逻辑
    /// </summary>
    public partial class RecipeTabControl : UserControl
    {
        public RecipeTabControl()
        {
            InitializeComponent();
        }

        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {

        }
    }
}
