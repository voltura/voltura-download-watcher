namespace VolturaDownloadWatcher;

public partial class ConfirmationDialog : System.Windows.Window
{
    public ConfirmationDialog(string message)
    {
        InitializeComponent();
        MessageText.Text = message;
    }

    private void Confirm_Click(object sender, System.Windows.RoutedEventArgs e) => DialogResult = true;

    private void Cancel_Click(object sender, System.Windows.RoutedEventArgs e) => DialogResult = false;

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key is System.Windows.Input.Key.Escape)
        {
            DialogResult = false;
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
