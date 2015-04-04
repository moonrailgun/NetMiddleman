using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Net.Sockets;

namespace NetMiddlemanClient
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public string NMipaddress;
        public int NMport;
        public int Listenport;

        private TcpClient NMClient;

        public MainWindow()
        {
            InitializeComponent();
            this.StopListenButton.IsEnabled = false;
        }

        private void OnListenClick(object sender, RoutedEventArgs e)
        {
            try
            {
                NMipaddress = this.NMipaddressTextBox.Text;
                NMport = Convert.ToInt32(this.NMportTextBox.Text);
                Listenport = Convert.ToInt32(this.ListenPortTextBox.Text);

                if (NMport > 65535 || Listenport > 65535 || NMport <= 0 || Listenport <= 0)
                {
                    this.StateLabel.Content = "端口号范围在0~65535";
                    return;
                }

                NMConnectStart();
            }
            catch (FormatException)
            {
                this.StateLabel.Content = "请输入合法的数字作为端口";
            }
        }

        private void NMConnectStart()
        {
            try
            {
                if (NMClient == null)
                {
                    NMClient = new TcpClient();
                }

                this.StartListenButton.IsEnabled = false;
                this.StopListenButton.IsEnabled = true;

                this.StateLabel.Content = "正在连接到远程服务器";
                NMClient.Connect(NMipaddress, NMport);
                this.StateLabel.Content = "连接成功";
            }
            catch(Exception ex)
            {
                this.StateLabel.Content = "无法初始化TCP连接";
            }
        }

        private void OnListenStop(object sender, RoutedEventArgs e)
        {
            NMClient.Close();
            NMClient = null;
            this.StartListenButton.IsEnabled = true;
            this.StopListenButton.IsEnabled = false;
            this.StateLabel.Content = "已关闭监听端口";
        }
    }
}
