using System.Net;
using System.Net.Sockets;
using System.Reflection.Emit;
using System.Text;
using System.Text.Json;
using WsSharp.Handlers;

namespace WsSharp;

public record WebSocketClient(IPEndPoint ConnectionAddress)
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

            byte[] decompressedBytes = await GZip.Decompress(dataStream.ToArray());
            await dataStream.DisposeAsync();

            for (int totalRead = 0; decompressedBytes.Length - totalRead > 0;)
            {
                uint replyId = GZip.Byte2UInt(decompressedBytes, totalRead + 4);
                int length = GZip.Byte2Int(decompressedBytes, totalRead + 8);

                string rawMessage = Encoding.UTF8.GetString(decompressedBytes, totalRead + 12, length);
                totalRead += length + 12;
                _packetIndex++;

                if (_replyTasks.TryGetValue(replyId, out var replyTask))
                {
                    _replyTasks.Remove(replyId);
                    replyTask.SetResult(rawMessage);
                }

                OnReceived?.Invoke(this, rawMessage);
            }
        }
    }

    /// <summary>
    /// Connects the socket client to the websocket server
    /// </summary>
    //Todo: In the future handle fragmentation.
    public void Connect()
    {
        Client.DontFragment = true;
        Client.Connect(ConnectionAddress);
        
        ReceiveMessages();
    }

    /// <summary>
    /// Compresses a string using Gzip then sends it to the server with no reply
    /// </summary>
    /// <param name="data"></param>
    public async Task Send(string data)
    {
        if (!Client.Connected)
            return;
        
        byte[] dataCompressed = GZip.Compress(data, _packetIndex);
        
        await Client.SendAsync(dataCompressed, SocketFlags.None);
    }

    /// <summary>
    /// Compresses a string using Gzip then sends it to the server after awaits for a reply from the server
    /// </summary>
    /// <param name="data"></param>
    /// <returns>a string reply from the server</returns>
    public async Task<string?> SendWithReply(string data)
    {
        if (!Client.Connected)
            return null;
        
        var replyTask = new TaskCompletionSource<string>();
        _replyTasks[_packetIndex] = replyTask;

        byte[] dataCompressed = GZip.Compress(data, _packetIndex);
        await Client.SendAsync(dataCompressed, SocketFlags.None);

        return await replyTask.Task;
    }
}