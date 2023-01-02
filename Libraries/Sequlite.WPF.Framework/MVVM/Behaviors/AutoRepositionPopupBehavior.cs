using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Interactivity;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace Sequlite.WPF.Framework
{
    public class AutoRepositionPopupBehavior : Behavior<Popup>
    {
        private const int WM_MOVING = 0x0216;

        // should be moved to a helper class
        private DependencyObject GetTopmostParent(DependencyObject element)
        {
            var current = element;
            var result = element;

            while (current != null)
            {
                result = current;
                current = (current is Visual || current is Visual3D) ?
                   VisualTreeHelper.GetParent(current) :
                   LogicalTreeHelper.GetParent(current);
            }
            return result;
        }

        protected override void OnAttached()
        {
            base.OnAttached();

            AssociatedObject.Loaded += (sender, e) =>
            {
                var root = GetTopmostParent(AssociatedObject.PlacementTarget) as Window;
                if (root != null)
                {
                    var helper = new WindowInteropHelper(root);
                    var hwndSource = HwndSource.FromHwnd(helper.Handle);
                    if (hwndSource != null)
                    {
                        hwndSource.AddHook(HwndMessageHook);
                    }
                }
            };
        }

        private IntPtr HwndMessageHook(IntPtr hWnd,
                int msg, IntPtr wParam,
                IntPtr lParam, ref bool bHandled)
        {
            if (msg == WM_MOVING)
            {
                Update();
            }
            return IntPtr.Zero;
        }

        public void Update()
        {
            // force the popup to update it's position
            var mode = AssociatedObject.Placement;
            AssociatedObject.Placement = PlacementMode.Relative;
            AssociatedObject.Placement = mode;
        }
    }
}
