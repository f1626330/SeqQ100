using Sequlite.ALF.EngineerGUI.ViewModel;
using System.Windows.Controls;

namespace Sequlite.ALF.EngineerGUI.View
{
    /// <summary>
    /// ImageContainerControl.xaml 的交互逻辑
    /// </summary>
    public partial class ImageContainerControl : UserControl
    {
        //private bool _IsAvalonLoaded;
        public ImageContainerControl()
        {
            InitializeComponent();
        }

        //private void _DockHost_AvalonDockLoaded(object sender, System.EventArgs e)
        //{
        //    if (!_IsAvalonLoaded)
        //    {
        //        string _AdDefaultLayoutResourceName = "Sequlite.ALF.EngineerGUI.AvalonDockDefaultLayout.xml";
        //        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        //        using (var stream = assembly.GetManifestResourceStream(_AdDefaultLayoutResourceName))
        //        {
        //            if (stream != null)
        //            {
        //                _DockHost.DockingManager.RestoreLayout(stream);
        //                _IsAvalonLoaded = true;
        //            }
        //        }
        //    }
        //}

        
    }
}
