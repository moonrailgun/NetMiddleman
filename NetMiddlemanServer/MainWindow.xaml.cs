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
using System.Threading;

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
        private TcpListener dataListener;
        private TcpClient remoteNMClient;
        private List<TcpClient> remoteDataClient = new List<TcpClient>();
        List<ArraySegment<byte>> recvBuffers = new List<ArraySegment<byte>>(2);
        byte[] bigBuffer = new byte[1024];

        public MainWindow()
        {
            InitializeComponent();
            this.StopListenButton.IsEnabled = false;

            recvBuffers.Add(new ArraySegment<byte>(bigBuffer, 4, 2));
            recvBuffers.Add(new ArraySegment<byte>(bigBuffer, 20, 500));
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
            catch (FormatException)
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
                NMListener.Start(10);
                this.StartListenButton.IsEnabled = false;
                this.StopListenButton.IsEnabled = true;
                this.StateLabel.Content = "等待接入连接";

                NMListener.BeginAcceptTcpClient(new AsyncCallback(OnAcceptTcpClient), NMListener);
            }
            catch (Exception)
            {
                this.StateLabel.Content = "无法初始化TCP连接";
            }
        }

        /// <summary>
        /// 关闭监听
        /// </summary>
        private void OnStopListen(object sender, RoutedEventArgs e)
        {
            //关闭所有的网络连接
            NMListener.Stop();
            dataListener.Stop();
            remoteNMClient.Close();
            if (remoteDataClient.Count != 0)
            {
                //关闭所有数据接口
                foreach (TcpClient client in remoteDataClient)
                {
                    client.Close();
                }
            }

            this.StartListenButton.IsEnabled = true;
            this.StopListenButton.IsEnabled = false;
            this.StateLabel.Content = "已关闭监听端口";
        }

        private void OnAcceptTcpClient(IAsyncResult ar)
        {
            try
            {
                TcpListener listener = (TcpListener)ar.AsyncState;

                remoteNMClient = listener.EndAcceptTcpClient(ar);
                ChangeLogContentInvoke("已创建连接,状态：" + remoteNMClient.Connected);

                CreateDataListener();
            }
            catch { }
        }

        private void CreateDataListener()
        {
            dataListener = new TcpListener(IPAddress.Parse("127.0.0.1"), Listenport);
            dataListener.Start();

            dataListener.BeginAcceptTcpClient(OnAcceptDataTcpClient, dataListener);
        }

        private void OnAcceptDataTcpClient(IAsyncResult ar)
        {
            try
            {
                TcpListener listener = (TcpListener)ar.AsyncState;

                TcpClient client = listener.EndAcceptTcpClient(ar);
                client.Client.BeginReceive(recvBuffers, SocketFlags.None, new AsyncCallback(receiveData), client);

                remoteDataClient.Add(client);
                LogsSystem.Instance.Print(string.Format("NetMiddleman已建立新的数据来源,目前一共有{0}个连接", remoteDataClient.Count));


                //循环接受TCP连接
                dataListener.BeginAcceptTcpClient(OnAcceptDataTcpClient, dataListener);
            }
            catch { }
        }

        /// <summary>
        /// 异步接收数据
        /// </summary>
        private void receiveData(IAsyncResult client)
        {
            // 调用异步方法 BeginReceive 来告知 socket 如何接收数据
            //IAsyncResult iar = client.BeginReceive(buffer, 0, BagSize, SocketFlags.None, out errorCode, receiveCallback, buffer);
        }

        #region 多线程写入控件委托
        private delegate void AddLogItemDelegate(Label label, string log);
        private void ChangeLogContent(Label label, string log)
        {
            label.Content = log;
        }

        private void ChangeLogContentInvoke(string log)
        {
            this.Dispatcher.BeginInvoke(new AddLogItemDelegate(ChangeLogContent), this.StateLabel, log);
        }
        #endregion
    }
}
