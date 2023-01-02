using Microsoft.WindowsAPICodePack.Dialogs;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;

//using System.Windows.Forms;
using System.Windows.Interactivity;
using Button = System.Windows.Controls.Button;

namespace Sequlite.WPF.Framework
{
    public class FolderDialogBehavior : Behavior<Button>
    {
        public string SetterName { get; set; }
        //public bool EnableCreateNewFolder { get; set; }
        public bool IsFolderPicker { get; set; } = true;

        public static DependencyProperty InitialDirProperty =
           DependencyProperty.RegisterAttached(
               "InitialDir", typeof(string), typeof(FolderDialogBehavior),
               new PropertyMetadata(""));
        public static string GetInitialDir(DependencyObject d)
        {
            return (string)d.GetValue(InitialDirProperty);
        }
        public static void SetInitialDir(DependencyObject d, string value)
        {
            d.SetValue(InitialDirProperty, value);
        }


        public static DependencyProperty TitleProperty =
           DependencyProperty.RegisterAttached(
               "Title", typeof(string), typeof(FolderDialogBehavior),
               new PropertyMetadata(""));
        public static string GetTitle(DependencyObject d)
        {
            return (string)d.GetValue(TitleProperty);
        }
        public static void SetTitle(DependencyObject d, string value)
        {
            d.SetValue(TitleProperty, value);
        }


        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.Click += OnClick;
        }

        protected override void OnDetaching()
        {
            AssociatedObject.Click -= OnClick;
        }

        
        private void OnClick(object sender, RoutedEventArgs e)
        {
            var dialog = new CommonOpenFileDialog();//  FolderBrowserDialog();
            string initialDir = GetInitialDir(this);
            dialog.AllowNonFileSystemItems = false;
            dialog.Multiselect = false;
            dialog.IsFolderPicker = IsFolderPicker;// true;
            dialog.Title = GetTitle(this);
            //dialog.ShowNewFolderButton = EnableCreateNewFolder;
            if (!string.IsNullOrEmpty(initialDir) && Directory.Exists(initialDir))
            {
                dialog.InitialDirectory = initialDir;
            }
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                var propertyInfo = AssociatedObject.DataContext.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
                    .Where(p => p.CanRead && p.CanWrite)
                    .First(p => p.Name.Equals(SetterName));
                propertyInfo.SetValue(AssociatedObject.DataContext, dialog.FileName, null);
            }
            //string initialDir = GetInitialDir(this);
            //var dialog =  new FolderBrowserDialog();
            //dialog.ShowNewFolderButton = EnableCreateNewFolder;
            //if (!string.IsNullOrEmpty(initialDir) && Directory.Exists(initialDir))
            //{
            //    dialog.SelectedPath = initialDir;
            //}
            //var result = dialog.ShowDialog();
            //if (result == DialogResult.OK && AssociatedObject.DataContext != null)
            //{
            //    var propertyInfo = AssociatedObject.DataContext.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
            //        .Where(p => p.CanRead && p.CanWrite)
            //        .First(p => p.Name.Equals(SetterName));

            //    propertyInfo.SetValue(AssociatedObject.DataContext, dialog.SelectedPath, null);
            //}
        }
    }
}
