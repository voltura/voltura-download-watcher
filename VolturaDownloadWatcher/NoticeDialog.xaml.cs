namespace VolturaDownloadWatcher;

public partial class NoticeDialog : System.Windows.Window
{
    public NoticeDialog(string message)
    {
        InitializeComponent();
        MessageText.Text = message;
    }

    private void Dismiss_Click(object sender, System.Windows.RoutedEventArgs e) => Close();

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key is System.Windows.Input.Key.Escape or System.Windows.Input.Key.Enter)
        {
            Close();
            e.Handled = true;
        }
    }

    private void Shell_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton is System.Windows.Input.MouseButton.Left)
        {
            DragMove();
        }
    }
}
