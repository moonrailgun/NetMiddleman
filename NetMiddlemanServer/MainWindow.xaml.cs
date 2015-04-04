using System;
using System.Collections.Generic;
using System.Linq;
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
using System.Net;

namespace NetMiddlemanServer
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public int NMport;
        public int Listenport;

        private TcpListener NMListener;

        public MainWindow()
        {
            InitializeComponent();
            this.StopListenButton.IsEnabled = false;
        }

        /// <summary>
        /// 按下开始监听按钮
        /// </summary>
        private void OnListenClick(object sender, RoutedEventArgs e)
        {
            try
            {
                NMport = Convert.ToInt32(this.NMportTextBox.Text);
                Listenport = Convert.ToInt32(this.ListenPortTextBox.Text);

                if (NMport > 65535 || Listenport > 65535 || NMport <= 0 || Listenport <= 0)
                {
                    this.StateLabel.Content = "端口号范围在0~65535";
                    return;
                }

                NMListenStart();
            }
            catch (FormatException fe)
            {
                this.StateLabel.Content = "请输入合法的数字作为端口";
            }

        }

        private void NMListenStart()
        {
            IPAddress ipAddress = IPAddress.Parse("127.0.0.1");
            try
            {
                NMListener = new TcpListener(ipAddress, NMport);
                NMListener.Start(1);
                this.StartListenButton.IsEnabled = false;
                this.StopListenButton.IsEnabled = true;
                this.StateLabel.Content = "等待接入连接";
            }
            catch (Exception ex)
            {
                this.StateLabel.Content = "无法初始化TCP连接";
            }
        }

        private void OnStopListen(object sender, RoutedEventArgs e)
        {
            NMListener.Stop();
            this.StartListenButton.IsEnabled = true;
            this.StopListenButton.IsEnabled = false;
            this.StateLabel.Content = "已关闭监听端口";
        }
    }
}
