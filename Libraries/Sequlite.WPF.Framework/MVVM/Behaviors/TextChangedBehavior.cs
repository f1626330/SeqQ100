using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Sequlite.WPF.Framework
{

    public class TextChangedBehavior
    {
        public static readonly DependencyProperty TextChangedCommandProperty =
            DependencyProperty.RegisterAttached("TextChangedCommand",
                                                typeof(ICommand),
                                                typeof(TextChangedBehavior),
                                                new UIPropertyMetadata(TextChangedCommandChanged));

        private static readonly DependencyProperty UserInputProperty =
            DependencyProperty.RegisterAttached("UserInput",
                                                typeof(bool),
                                                typeof(TextChangedBehavior));

        public static void SetTextChangedCommand(DependencyObject target, ICommand value)
        {
            target.SetValue(TextChangedCommandProperty, value);
        }

        private static void ExecuteTextChangedCommand(TextBox sender, TextChangedEventArgs e)
        {
            var command = (ICommand)sender.GetValue(TextChangedCommandProperty);
            var arguments = new object[] { sender, e, GetUserInput(sender) };
            command.Execute(arguments);
        }

        private static bool GetUserInput(DependencyObject target)
        {
            return (bool)target.GetValue(UserInputProperty);
        }

        private static void SetUserInput(DependencyObject target, bool value)
        {
            target.SetValue(UserInputProperty, value);
        }

        private static void TextBoxOnPreviewExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            if (e.Command != ApplicationCommands.Cut)
            {
                return;
            }

            var textBox = sender as TextBox;
            if (textBox == null)
            {
                return;
            }

            SetUserInput(textBox, true);
        }

        private static void TextBoxOnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            var textBox = (TextBox)sender;
            switch (e.Key)
            {
                case Key.Return:
                    if (textBox.AcceptsReturn)
                    {
                        SetUserInput(textBox, true);
                    }
                    break;

                case Key.Delete:
                    if (textBox.SelectionLength > 0 || textBox.SelectionStart < textBox.Text.Length)
                    {
                        SetUserInput(textBox, true);
                    }
                    break;

                case Key.Back:
                    if (textBox.SelectionLength > 0 || textBox.SelectionStart > 0)
                    {
                        SetUserInput(textBox, true);
                    }
                    break;
            }
        }

        private static void TextBoxOnPreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            SetUserInput((TextBox)sender, true);
        }

        private static void TextBoxOnTextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = (TextBox)sender;
            ExecuteTextChangedCommand(textBox, e);
            SetUserInput(textBox, false);
        }

        private static void TextBoxOnTextPasted(object sender, DataObjectPastingEventArgs e)
        {
            var textBox = (TextBox)sender;
            if (e.SourceDataObject.GetDataPresent(DataFormats.Text, true) == false)
            {
                return;
            }

            SetUserInput(textBox, true);
        }

        private static void TextChangedCommandChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
        {
            var textBox = target as TextBox;
            if (textBox == null)
            {
                return;
            }

            if (e.OldValue != null)
            {
                textBox.PreviewKeyDown -= TextBoxOnPreviewKeyDown;
                textBox.PreviewTextInput -= TextBoxOnPreviewTextInput;
                CommandManager.RemovePreviewExecutedHandler(textBox, TextBoxOnPreviewExecuted);
                DataObject.RemovePastingHandler(textBox, TextBoxOnTextPasted);
                textBox.TextChanged -= TextBoxOnTextChanged;
            }

            if (e.NewValue != null)
            {
                textBox.PreviewKeyDown += TextBoxOnPreviewKeyDown;
                textBox.PreviewTextInput += TextBoxOnPreviewTextInput;
                CommandManager.AddPreviewExecutedHandler(textBox, TextBoxOnPreviewExecuted);
                DataObject.AddPastingHandler(textBox, TextBoxOnTextPasted);
                textBox.TextChanged += TextBoxOnTextChanged;
            }
        }
    }
}
