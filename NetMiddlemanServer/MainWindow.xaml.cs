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
        private Dictionary<Socket, Socket> NMlist = new Dictionary<Socket, Socket>();//<NM互通连接,数据连接>NM互通通信与数据通信对应字典

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
            catch (FormatException)
            {
                this.StateLabel.Content = "请输入合法的数字作为端口";
            }

        }

        //打开NM监听端口
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
            try
            {
                //关闭所有的网络连接
                NMListener.Stop();
                dataListener.Stop();
                remoteNMClient.Close();
                if (NMlist.Count != 0)
                {
                    //关闭所有数据接口
                    foreach (KeyValuePair<Socket, Socket> kvp in NMlist)
                    {
                        kvp.Key.Close();
                        kvp.Value.Close();
                    }
                }

                this.StartListenButton.IsEnabled = true;
                this.StopListenButton.IsEnabled = false;
                this.StateLabel.Content = "已关闭监听端口";
            }
            catch (Exception ex)
            {
                LogsSystem.Instance.Print(ex.ToString(), LogLevel.ERROR);
            }
        }

        /// <summary>
        /// 回调函数
        /// 当有连接接入后调用
        /// </summary>
        private void OnAcceptTcpClient(IAsyncResult ar)
        {
            try
            {
                //接受监听到的数据
                TcpListener listener = (TcpListener)ar.AsyncState;
                remoteNMClient = listener.EndAcceptTcpClient(ar);
                ChangeLogContentInvoke("已创建连接,状态：" + remoteNMClient.Connected);
                //Receive(remoteNMClient.Client);

                //继续接受下一个连接
                NMListener.BeginAcceptTcpClient(new AsyncCallback(OnAcceptTcpClient), NMListener);

                //创建数据流监听
                CreateDataListener();
            }
            catch (Exception e) { LogsSystem.Instance.Print(e.ToString(), LogLevel.ERROR); }
        }

        //开始监听数据流TCP连接
        private void CreateDataListener()
        {
            dataListener = new TcpListener(IPAddress.Parse("127.0.0.1"), Listenport);
            dataListener.Start();

            dataListener.BeginAcceptTcpClient(OnAcceptDataTcpClient, dataListener);
        }

        /// <summary>
        /// 回调函数
        /// 当有数据流TCP连接接入
        /// </summary>
        private void OnAcceptDataTcpClient(IAsyncResult ar)
        {
            try
            {
                //处理接收到的socket连接
                TcpListener listener = (TcpListener)ar.AsyncState;
                TcpClient client = listener.EndAcceptTcpClient(ar);

                TcpClient NMClient = new TcpClient();
                NMClient.Connect((IPEndPoint)remoteNMClient.Client.RemoteEndPoint);//创建一个连接到远程的TCP协议
                NMlist.Add(NMClient.Client, client.Client);//添加到字典

                Receive(client.Client);//开始接受数据
                LogsSystem.Instance.Print(string.Format("NetMiddleman已建立新的数据来源,目前一共有{0}个连接", NMlist.Count));


                //循环监听TCP连接
                dataListener.BeginAcceptTcpClient(OnAcceptDataTcpClient, dataListener);
            }
            catch (Exception e)
            {
                LogsSystem.Instance.Print(e.ToString(), LogLevel.ERROR);
            }
        }

        #region 发送数据
        private void Send(Socket socket, byte[] data)
        {
            LogsSystem.Instance.Print("发送数据:" + Encoding.ASCII.GetString(data));
            socket.BeginSend(data, 0, data.Length, 0, new AsyncCallback(SendCallback), socket);
        }
        private void SendCallback(IAsyncResult ar)
        {
            try
            {
                Socket handler = (Socket)ar.AsyncState;

                int bytesSent = handler.EndSend(ar);
                LogsSystem.Instance.Print("发送完成");
            }
            catch (Exception e)
            {
                LogsSystem.Instance.Print(e.ToString(), LogLevel.ERROR);
            }
        }
        #endregion

        #region 接受数据
        private void Receive(Socket client)
        {
            try
            {
                StateObject state = new StateObject();
                state.socket = client;
                client.BeginReceive(state.buffer, 0, StateObject.buffSize, 0, new AsyncCallback(ReceiveCallback), state);
            }
            catch (Exception e)
            {
                LogsSystem.Instance.Print(e.ToString(), LogLevel.ERROR);
            }
        }
        private void ReceiveCallback(IAsyncResult ar)
        {
            try
            {
                StateObject receiveState = (StateObject)ar.AsyncState;
                Socket client = receiveState.socket;

                int bytesRead = client.EndReceive(ar);
                if (bytesRead < StateObject.buffSize)
                {
                    //如果读取到数据长度较小
                    receiveState.dataByte.AddRange(receiveState.buffer);//将缓存加入结果列
                    receiveState.buffer = new byte[StateObject.buffSize];//清空缓存

                    //接受完成
                    byte[] receiveData = receiveState.dataByte.ToArray();
                    Console.WriteLine("接受到{0}字节数据", receiveData.Length);
                    //处理数据
                    ProcessReceiveMessage(receiveData, client.LocalEndPoint, receiveState.socket);

                    Receive(client);//继续下一轮的接受
                }
                else
                {
                    //如果读取到数据长度大于缓冲区
                    receiveState.dataByte.AddRange(receiveState.buffer);//将缓存加入结果列
                    receiveState.buffer = new byte[StateObject.buffSize];//清空缓存
                    client.BeginReceive(receiveState.buffer, 0, StateObject.buffSize, 0, new AsyncCallback(ReceiveCallback), receiveState);//继续接受下一份数据包
                }
            }
            catch (Exception e)
            {
                LogsSystem.Instance.Print(e.ToString(), LogLevel.ERROR);
            }
        }
        #endregion

        /// <summary>
        /// 处理接受到的数据
        /// </summary>
        /// <param name="message">接收到的数据</param>
        /// <param name="fromPort">接受的本地端口</param>
        private void ProcessReceiveMessage(byte[] message, EndPoint localEndPoint, Socket socket)
        {
            int port = ((IPEndPoint)localEndPoint).Port;

            if (port == NMport)
            {
                //如果消息来自NM通信
                //转发给远程
                if(NMlist.ContainsKey(socket))
                {
                    Socket NMSocket = NMlist[socket];
                    Send(NMSocket, message);
                }
            }
            else if(port == Listenport)
            {
                //如果消息来自远程通信
                //转发给NM
                if(NMlist.ContainsValue(socket))
                {
                    foreach(KeyValuePair<Socket,Socket> kvp in NMlist)
                    {
                        if(kvp.Value == socket)
                        {
                            //找到对应的socket对象
                            Send(kvp.Value, message);
                            break;//跳出循环
                        }
                    }
                }
            }
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

    class StateObject
    {
        //socket 客户端
        public Socket socket = null;
        //缓冲区大小
        public const int buffSize = 256;
        //缓冲
        public byte[] buffer = new byte[buffSize];
        //数据流
        public List<byte> dataByte = new List<byte>();
    }
}
