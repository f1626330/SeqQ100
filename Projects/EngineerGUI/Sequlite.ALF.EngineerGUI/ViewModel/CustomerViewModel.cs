using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using Sequlite.ALF.RecipeLib;
using Sequlite.WPF.Framework;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Input;

namespace Sequlite.ALF.EngineerGUI.ViewModel
{
    public class CustomerViewModel : ViewModelBase
    {
        private bool _simulation;

        public bool Simulation
        {
            get
            {
                return _simulation;
            }
            set
            {
                _simulation = value;
            }
        }
    }
}
