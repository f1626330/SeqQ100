
using System.Threading;
using System.Windows;

namespace Sequlite.ALF.EngineerGUI
{
    public partial class MainWindow : Window
    {
        internal MainWindow() 
        {
            InitializeComponent();
            Thread.CurrentThread.Name = "EUI";
            Title += string.Format(" V{0}", System.Reflection.Assembly.GetExecutingAssembly().GetName().Version);
        }

      
    }
}
