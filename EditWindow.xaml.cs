using System.Windows;

namespace ShopWalletApp
{
    public partial class EditWindow : Window
    {
        public string NewValue { get; private set; }
        public EditWindow(string oldValue)
        {
            InitializeComponent();
            NewValue = oldValue ?? string.Empty;
            ValueBox.Text = NewValue;
            Loaded += (_, _) => { ValueBox.Focus(); ValueBox.SelectAll(); };
        }
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (!decimal.TryParse(ValueBox.Text, out decimal amount) || amount <= 0m)
            {
                ErrorText.Text = "Enter an amount greater than zero.";
                return;
            }
            NewValue = ValueBox.Text;
            DialogResult = true;
        }
    }
}
