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

//内网端
namespace NetMiddlemanClient
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public string NMipaddress;
        public int NMport;
        public int localDataPort;
        public Dictionary<Socket, Socket> NMlist = new Dictionary<Socket, Socket>();//<NM互通连接,数据连接>NM互通通信与数据通信对应字典

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
                localDataPort = Convert.ToInt32(this.ListenPortTextBox.Text);

                if (NMport > 65535 || localDataPort > 65535 || NMport <= 0 || localDataPort <= 0)
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

        /// <summary>
        /// 开始服务
        /// </summary>
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
                NMClient.Connect(NMipaddress, NMport);//同步阻塞
                this.StateLabel.Content = "连接成功";

                CreateDataListener();
            }
            catch(Exception)
            {
                this.StateLabel.Content = "无法初始化TCP连接";
            }
        }

        //开始监听数据流TCP连接
        private void CreateDataListener()
        {
            TcpListener dataListener = new TcpListener(IPAddress.Parse("127.0.0.1"), NMport);
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
                TcpClient client = listener.EndAcceptTcpClient(ar);//NM用

                TcpClient localDataClient = new TcpClient();
                localDataClient.Connect(IPAddress.Parse("127.0.0.1"), localDataPort);//创建一个连接到远程的TCP协议
                NMlist.Add(client.Client, localDataClient.Client);//添加到字典

                Receive(client.Client);//开始接受数据
                LogsSystem.Instance.Print(string.Format("NetMiddleman已建立新的数据来源,目前一共有{0}个连接", NMlist.Count));


                //循环监听TCP连接
                listener.BeginAcceptTcpClient(OnAcceptDataTcpClient, listener);
            }
            catch (Exception e)
            {
                LogsSystem.Instance.Print(e.ToString(), LogLevel.ERROR);
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
                //转发给本地
                if (NMlist.ContainsKey(socket))
                {
                    Socket NMSocket = NMlist[socket];
                    Send(NMSocket, message);
                }
            }
            else if (port == localDataPort)
            {
                //如果消息来自本地
                //转发给远程
                if (NMlist.ContainsValue(socket))
                {
                    foreach (KeyValuePair<Socket, Socket> kvp in NMlist)
                    {
                        if (kvp.Value == socket)
                        {
                            //找到对应的socket对象
                            Send(kvp.Value, message);
                            break;//跳出循环
                        }
                    }
                }
            }
        }
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
