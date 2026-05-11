using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Layout;
using System.Threading.Tasks;

namespace PixelWrench
{
    public partial class MessageBoxCustom : Window
    {
        public enum MessageBoxButtons { Ok, YesNo, OkOpenFolder }
        public enum MessageBoxResult { Ok, Cancel, Yes, No, OpenFolder }

        public MessageBoxResult Result { get; private set; } = MessageBoxResult.Cancel;

        public MessageBoxCustom() => InitializeComponent();

        public static async Task<MessageBoxResult> Show(Avalonia.Visual parent, string text, string title, MessageBoxButtons buttons = MessageBoxButtons.Ok, bool isSelectable = false, string selectableText = null)
        {
            var msgbox = new MessageBoxCustom();
            var parentWindow = Avalonia.Controls.TopLevel.GetTopLevel(parent) as Window;
            msgbox.TitleBlock.Text = title.ToUpper();
            
            msgbox.MessageBlock.Text = text;
            msgbox.MessageBlock.IsVisible = true;

            if (isSelectable && !string.IsNullOrEmpty(selectableText)) {
                msgbox.SelectableMessageBlock.Text = selectableText;
                msgbox.SelectableMessageBlock.IsVisible = true;
            } else if (isSelectable) {
                msgbox.SelectableMessageBlock.Text = text;
                msgbox.SelectableMessageBlock.IsVisible = true;
                msgbox.MessageBlock.IsVisible = false;
            } else {
                msgbox.SelectableMessageBlock.IsVisible = false;
            }

            if (buttons == MessageBoxButtons.Ok)
                AddButton(msgbox, "OK", MessageBoxResult.Ok, true);
            else if (buttons == MessageBoxButtons.YesNo)
            {
                AddButton(msgbox, "Yes", MessageBoxResult.Yes, true);
                AddButton(msgbox, "No", MessageBoxResult.No, false);
            }
            else if (buttons == MessageBoxButtons.OkOpenFolder)
            {
                AddButton(msgbox, "Open Folder", MessageBoxResult.OpenFolder, true);
                AddButton(msgbox, "OK", MessageBoxResult.Ok, false);
            }

            if (parentWindow != null)
                await msgbox.ShowDialog(parentWindow);
            else 
                return MessageBoxResult.Ok;
            
            return msgbox.Result;
        }

        private static void AddButton(MessageBoxCustom msgbox, string text, MessageBoxResult result, bool isPrimary)
        {
            var btn = new Button 
            { 
                Content = text, Width = text.Length > 5 ? 120 : 80, HorizontalContentAlignment = HorizontalAlignment.Center,
                Background = isPrimary ? new SolidColorBrush(Color.Parse("#8b5cf6")) : new SolidColorBrush(Color.Parse("#413659")),
                Foreground = Brushes.White, CornerRadius = new CornerRadius(4), Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
            };
            btn.Click += (_, __) => { msgbox.Result = result; msgbox.Close(); };
            msgbox.ButtonsPanel.Children.Add(btn);
        }
    }
}