using System.Net;
using System.Net.Sockets;
using System.Text;

namespace WsSharp;

/// <summary>
/// Used when connecting to external websockets, this is due to the fact that the internal WsSharp to WsSharp communication has custom packets which other services may not understand
/// </summary>
/// <param name="ConnectionAddress"></param>
//Todo: Some day create an abstraction layer around all of this 
public record WebSocketClientExternal(IPEndPoint ConnectionAddress)
{
    private readonly Dictionary<uint, TaskCompletionSource<string>> _replyTasks = new();

    private static readonly Socket Client = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    
    private static uint _packetIndex = 1;

    public event EventHandler<string> OnReceived;

    private async void ReceiveMessages()
    {
        byte[] localBuffer = new byte[512];

        while (Client.Connected)
        {
            MemoryStream dataStream = new();

            do
            {
                int received = await Client.ReceiveAsync(localBuffer, SocketFlags.None);
                dataStream.Write(localBuffer, 0, received);
            } while (Client.Available > 0);

            string rawMessage = Encoding.UTF8.GetString(dataStream.ToArray());
            
            OnReceived?.Invoke(this, rawMessage);
        }
    }

    /// <summary>
    /// Connects the socket client to the websocket server
    /// </summary>
    public void Connect()
    {
        Client.DontFragment = true;
        Client.Connect(ConnectionAddress);
        
        ReceiveMessages();
    }
    /// <summary>
    /// Sends raw bytes to the server with no reply
    /// </summary>
    /// <param name="data"></param>
    public async Task Send(byte[] data)
    {
        if (!Client.Connected)
            return;
        
        await Client.SendAsync(data, SocketFlags.None);
    }

    /// <summary>
    /// Sends raw bytes to the server then awaits for a reply from the server
    /// </summary>
    /// <param name="data"></param>
    /// <returns>A string reply from the server</returns>
    public async Task<string?> SendWithReply(byte[] data)
    {
        if (!Client.Connected)
            return null;
        
        var replyTask = new TaskCompletionSource<string>();
        _replyTasks[_packetIndex] = replyTask;

        await Client.SendAsync(data, SocketFlags.None);

        return await replyTask.Task;
    }
}