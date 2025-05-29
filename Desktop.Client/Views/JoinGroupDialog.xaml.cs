using System.Windows;

namespace Desktop.Client.Views
{
    public partial class JoinGroupDialog : Window
    {
        public string GroupId { get; private set; }

        public JoinGroupDialog()
        {
            InitializeComponent();
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            GroupId = GroupIdBox.Text.Trim();
            if (string.IsNullOrEmpty(GroupId))
            {
                MessageBox.Show("请输入群聊ID");
                return;
            }
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
} 