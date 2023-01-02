using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Sequlite.WPF.Framework
{
	public interface IDialogBoxViewModel
	{
	}
	public interface IUserDialogBoxViewModel : IDialogBoxViewModel
	{
		bool IsModal { get; }
		void RequestClose();
		event EventHandler DialogBoxClosing;
	}

}
