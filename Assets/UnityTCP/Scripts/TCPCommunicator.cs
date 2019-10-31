using System;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Kodai100.Tcp
{

    public class TcpCommunicator : IDisposable
    {

        public string Name { get { return null==Socket?"Offline":Socket.RemoteEndPoint.ToString(); } }
      //  public string Name { get; }

        public bool IsConnecting
        {
            get
            {
                try
                {
                    if ((TcpClient == null) || !TcpClient.Connected) return false;
                    if (Socket == null) return false;

                    return !(Socket.Poll(1, SelectMode.SelectRead) && (Socket.Available <= 0));
                }
                catch
                {
                    return false;
                }
            }
        }


        private TcpClient TcpClient { get; } 

        private Socket Socket => TcpClient?.Client;

        private SynchronizationContext mainContext;
        private OnMessageEvent OnMessage;


        private bool running = false;
        string host; int port;

        public TcpCommunicator(TcpClient tcpClient, OnMessageEvent onMessage)
        {
            Debug.Log(1);
            this.TcpClient = tcpClient ?? throw new ArgumentNullException(nameof(tcpClient));

         //  this.Name = $"[{Socket.RemoteEndPoint}]";

            this.mainContext = SynchronizationContext.Current;
            this.OnMessage = onMessage;

        }

        //public TcpCommunicator(string host, int port, OnMessageEvent onMessage) : this(new TcpClient(host, port), onMessage)
        public TcpCommunicator(string host, int port, OnMessageEvent onMessage) : this(new TcpClient(), onMessage)
        {
            Debug.Log(2);
            this.host = host;
            this.port = port;
            ProcessIncomingNetworkData();
        }

        async void ProcessIncomingNetworkData()
        {
            byte[] buffer = new byte[1024];
            await this.TcpClient.ConnectAsync(host, port);
            Debug.Log($"RemoteEndPoint [{Socket.RemoteEndPoint}]");
            while (this.TcpClient.Connected)
            {
                try
                {
                    int count = await TcpClient.GetStream().ReadAsync(buffer, 0, buffer.Length);
                    ProcessIncomingData(buffer, count);
                }
                catch (Exception e)
                {
                    Debug.Log(e+ " IsConnecting: " + IsConnecting);
                    throw;
                }
            }
        }

        private void ProcessIncomingData(byte[] buffer, int count)
        {
            Debug.Log("receiving data " + buffer.Length + " : " + count);
            string str = Encoding.UTF8.GetString(buffer,0,count);
            mainContext.Post(_ => OnMessage.Invoke(str), null);
        }

        public void Dispose()
        {
            if (TcpClient != null)
            {
                running = false;
                TcpClient.Close();
                TcpClient.Dispose();

            }
        }



        public void Send(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (!IsConnecting) throw new InvalidOperationException();

            try
            {
                var stream = TcpClient.GetStream();
                stream.Write(data, 0, data.Length);
            }
            catch (Exception ex)
            {
                throw new ApplicationException("Attempt to send failed.", ex);
            }
        }


        public async Task Listen()
        {

            if (TcpClient == null) return;

            running = true;

            while (running)
            {
                await Task.Run(Receive);
            }

        }

        public async Task Receive()
        {
            if (!IsConnecting)
            {
                throw new InvalidOperationException();
            }

            try
            {
                var stream = TcpClient.GetStream();

                while (stream.DataAvailable)
                {
                    var reader = new StreamReader(stream, Encoding.UTF8);

                    var str = await reader.ReadLineAsync();

                    mainContext.Post(_ => OnMessage.Invoke(str), null);
                }

            }
            catch (Exception ex)
            {
                throw new ApplicationException("Attempt to receive failed.", ex);
            }
        }

    }
}
