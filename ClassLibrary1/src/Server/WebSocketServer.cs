using System.Net;
using System.Net.Sockets;
using System.Reflection.Emit;
using System.Text;

using WsSharp;
using WsSharp.Handlers;

namespace WsSharp;

public class SocketServer : IDisposable
{
    private static readonly CancellationTokenSource Cts = new();

    private static readonly HashSet<EndPoint> ConnectedIps = new();

    private static readonly Socket Listener = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    private static readonly IPEndPoint EndPoint = new(IPAddress.Loopback, 8787);

    private SocketState _state = SocketState.Undefined;

    private bool CanRun() => !Cts.Token.IsCancellationRequested && _state is SocketState.Connected;

    private static DateTime GetCurrentTime => DateTime.Now;
    
    public event EventHandler<SocketUser> OnConnected;

    public async Task Start()
    {
        Listener.Bind(EndPoint);
        Listener.Listen(32);

        _state = SocketState.Connected;

        while (CanRun())
        {
            Socket socket = await Listener.AcceptAsync();
            EndPoint? ip = socket.RemoteEndPoint;

            SocketUser socketUser = new(socket);
            if (ip == null || ConnectedIps.Contains(ip))
            {
                socket.Close();
                continue;
            }

            ConnectedIps.Add(ip);
            OnConnected?.Invoke(this, socketUser);
            
            _ = Task.Run(() => VirtualUserHandler(socketUser), socketUser.UserCancellation.Token);
        }
    }

    //Todo: Implement rate-limit
    private async Task VirtualUserHandler(SocketUser socketUser)
    {
        byte[] localBuffer = new byte[512];
        
        uint packetId = 1;
        
        while (CanRun())
        {
            MemoryStream dataStream = new();

            do
            {
                int totalReceived = await socketUser.UnderSocket.ReceiveAsync(localBuffer, SocketFlags.None);
                dataStream.Write(localBuffer, 0, totalReceived);
            } while (socketUser.UnderSocket.Available > 0);

            byte[] decompressedBytes = await GZip.Decompress(dataStream.ToArray());
            await dataStream.DisposeAsync();

            for (int totalRead = 0; decompressedBytes.Length - totalRead > 0; packetId++)
            {
                //move this to the deserializer
                socketUser.ReplyId = GZip.Byte2UInt(decompressedBytes, totalRead);
                int length = GZip.Byte2Int(decompressedBytes, totalRead + 8);

                string rawMessage = Encoding.UTF8.GetString(decompressedBytes, totalRead + 12, length);
                totalRead += length + 12;
                
                //socketUser.OnReceived?
                //Todo: Implement into API like structure
            }
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing) 
            return;
        
        Listener.Dispose();
        Cts.Dispose();

        ConnectedIps.Clear();
    }
}