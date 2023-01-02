using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Sequlite.WPF.Framework
{
	public static class DialogBoxBehavior
	{
		private static Dictionary<IDialogBoxViewModel, Window> DialogBoxes = new Dictionary<IDialogBoxViewModel, Window>();
		private static Dictionary<Window, NotifyCollectionChangedEventHandler> ChangeNotificationHandlers = new Dictionary<Window, NotifyCollectionChangedEventHandler>();
		private static Dictionary<ObservableCollection<IDialogBoxViewModel>, List<IDialogBoxViewModel>> DialogBoxViewModels = new Dictionary<ObservableCollection<IDialogBoxViewModel>, List<IDialogBoxViewModel>>();

		public static readonly DependencyProperty ClosingProperty = DependencyProperty.RegisterAttached(
			"Closing",
			typeof(bool),
			typeof(DialogBoxBehavior),
			new PropertyMetadata(false));

		public static readonly DependencyProperty ClosedProperty = DependencyProperty.RegisterAttached(
			"Closed",
			typeof(bool),
			typeof(DialogBoxBehavior),
			new PropertyMetadata(false));

		public static readonly DependencyProperty DialogViewModelsProperty = DependencyProperty.RegisterAttached(
			"DialogViewModels",
			typeof(object),
			typeof(DialogBoxBehavior),
			new PropertyMetadata(null, OnDialogViewModelsChange));

		public static void SetDialogViewModels(DependencyObject source, object value)
		{
			source.SetValue(DialogViewModelsProperty, value);
		}

		public static object GetDialogViewModels(DependencyObject source)
		{
			return source.GetValue(DialogViewModelsProperty);
		}

		private static void OnDialogViewModelsChange(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			if (d == null)
				return;
			var ele = d as FrameworkElement;
            var parent = ele as Window;
            for (int i = 0; i < 100 && parent == null; i++)
            {
                parent = ele as Window;
                if (parent == null)
                    ele = ele.Parent as FrameworkElement;
            }
			
            if (parent == null)
                return;

			parent.Closed += (s, a) => ChangeNotificationHandlers.Remove(parent);

			if (!ChangeNotificationHandlers.ContainsKey(parent))
				ChangeNotificationHandlers[parent] = (sender, args) =>
				{
					var collection = sender as ObservableCollection<IDialogBoxViewModel>;
					if (collection != null)
					{
						if (args.Action == NotifyCollectionChangedAction.Add ||
							args.Action == NotifyCollectionChangedAction.Remove || 
							args.Action == NotifyCollectionChangedAction.Replace)
						{
							if (args.NewItems != null)
								foreach (IDialogBoxViewModel viewModel in args.NewItems)
								{
									if (!DialogBoxViewModels.ContainsKey(collection))
										DialogBoxViewModels[collection] = new List<IDialogBoxViewModel>();
									DialogBoxViewModels[collection].Add(viewModel);
									AddDialog(viewModel, collection, parent as Window);
								}
							if (args.OldItems != null)
								foreach (IDialogBoxViewModel viewModel in args.OldItems)
								{
									RemoveDialog(viewModel);
									if (DialogBoxViewModels.Count > 0)
									{
										DialogBoxViewModels[collection].Remove(viewModel);
									}
									if (DialogBoxViewModels.Count > 0 && DialogBoxViewModels[collection].Count == 0)
										DialogBoxViewModels.Remove(collection);
								}
						}
						else if (args.Action == NotifyCollectionChangedAction.Reset)
						{
							
							if (DialogBoxViewModels.ContainsKey(collection))
							{
								var viewModels = DialogBoxViewModels[collection];
								if (viewModels != null)
								{
									foreach (var viewModel in DialogBoxViewModels[collection])
										RemoveDialog(viewModel);
								}
								DialogBoxViewModels.Remove(collection);
							}
						}
					}
				};

			
			var newCollection = e.NewValue as ObservableCollection<IDialogBoxViewModel>;
			if (newCollection != null)
			{
				newCollection.CollectionChanged += ChangeNotificationHandlers[parent];
				foreach (IDialogBoxViewModel viewModel in newCollection.ToList())
					AddDialog(viewModel, newCollection, parent as Window);
			}

			
			var oldCollection = e.OldValue as ObservableCollection<IDialogBoxViewModel>;
			if (oldCollection != null)
			{
				oldCollection.CollectionChanged -= ChangeNotificationHandlers[parent];
				foreach (IDialogBoxViewModel viewModel in oldCollection.ToList())
					RemoveDialog(viewModel);
			}
		}

		private static void AddDialog(IDialogBoxViewModel viewModel, ObservableCollection<IDialogBoxViewModel> collection, Window owner)
		{
			
            var viewModelType = viewModel.GetType();
			var resource = Application.Current.TryFindResource(viewModel.GetType());
            if (resource == null)
            {
                resource = Application.Current.MainWindow.TryFindResource(viewModel.GetType());
            }

           
            if (resource == null && owner != null)
            {
                resource = owner.TryFindResource(viewModel.GetType());
            }

            if (resource == null)
            {
                return;
            }

			if (IsGenericType(resource.GetType(), typeof(IDialogBoxView<>)))
			{
				resource.GetType().GetMethod("Show").Invoke(resource, new object[] { viewModel, owner });
				collection.Remove(viewModel);
			}

			{
				var userViewModel = viewModel as IUserDialogBoxViewModel;
				if (userViewModel == null)
					return;
				var dialog = resource as Window;
				dialog.DataContext = userViewModel;
				DialogBoxes[userViewModel] = dialog;
				userViewModel.DialogBoxClosing += (sender, args) =>
				{
					collection.Remove(sender as IUserDialogBoxViewModel);
				};

				dialog.Closing += (sender, args) =>
				{
					if (!(bool)dialog.GetValue(ClosingProperty))
					{
						dialog.SetValue(ClosingProperty, true);
						userViewModel.RequestClose();
						if (!(bool)dialog.GetValue(ClosedProperty))
						{
							args.Cancel = true;
							dialog.SetValue(ClosingProperty, false);
						}
					}
				};
				dialog.Closed += (sender, args) =>
				{
					Debug.Assert(DialogBoxes.ContainsKey(userViewModel));
					DialogBoxes.Remove(userViewModel);
					return;
				};
				dialog.Owner = owner;
                if (userViewModel.IsModal)
                {
                    dialog.ShowDialog();
                }
                else
                {
					if (dialog.WindowState == WindowState.Minimized)
					{
						dialog.WindowState = WindowState.Normal;
					}
					dialog.Show();
                    dialog.Activate();
                }
			}
		}

		private static void RemoveDialog(IDialogBoxViewModel viewModel)
		{
			if (DialogBoxes.ContainsKey(viewModel))
			{
				var dialog = DialogBoxes[viewModel];
				if (!(bool)dialog.GetValue(ClosingProperty))
				{
					dialog.SetValue(ClosingProperty, true);
					DialogBoxes[viewModel].Close();
				}
				dialog.SetValue(ClosedProperty, true);
			}
		}
		

		
		private static bool CanAssignToGenericType(Type givenType, Type genericType)
		{
			var interfaceTypes = givenType.GetInterfaces();

			foreach (var it in interfaceTypes)
			{
				if (it.IsGenericType && it.GetGenericTypeDefinition() == genericType)
					return true;
			}

			if (givenType.IsGenericType && givenType.GetGenericTypeDefinition() == genericType)
				return true;

			Type baseType = givenType.BaseType;
			if (baseType == null) return false;

			return CanAssignToGenericType(baseType, genericType);
		}

		private static bool IsGenericType(Type givenType, Type genericType)
		{
			var interfaceTypes = givenType.GetInterfaces();

			foreach (var it in interfaceTypes)
			{
				if (it.IsGenericType && it.GetGenericTypeDefinition() == genericType)
					return true;
			}

			if (givenType.IsGenericType && givenType.GetGenericTypeDefinition() == genericType)
				return true;

			Type baseType = givenType.BaseType;
			if (baseType == null) return false;

			return IsGenericType(baseType, genericType);
		}
	}

}
