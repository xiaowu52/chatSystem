using System.Windows;

namespace Desktop.Client.Views
{
    public partial class CreateGroupDialog : Window
    {
        public string GroupName { get; private set; }

        public CreateGroupDialog()
        {
            InitializeComponent();
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            GroupName = GroupNameBox.Text.Trim();
            if (string.IsNullOrEmpty(GroupName))
            {
                MessageBox.Show("请输入群聊名称");
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