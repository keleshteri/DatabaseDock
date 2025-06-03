using System.Windows;
using System.Diagnostics;
using System.Windows.Navigation;

namespace DatabaseDock
{
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
            // Find Hyperlink elements by iterating or naming them in XAML if more complex handling is needed.
            // For simple mailto, the XAML handles it. If process start is needed for other links:
            // Add RequestNavigate="Hyperlink_RequestNavigate" to Hyperlink in XAML
        }

        // Example handler if you add RequestNavigate to Hyperlinks:
        // private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        // {
        //     Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        //     e.Handled = true;
        // }
    }
}
