using System.Net.Sockets;
using System.Reflection.Emit;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WsSharp;

public record SocketUser(Socket UnderSocket) : IDisposable
{
    public readonly CancellationTokenSource UserCancellation = new CancellationTokenSource();
    
    private uint _packetId = 1;
    internal uint ReplyId = 1;

    public void Dispose()
    {
        UserCancellation.Cancel();
        UnderSocket.Close();

        UserCancellation.Dispose();

        GC.SuppressFinalize(this);
    }

    public async Task SendData(string message)
    {
        if (!UnderSocket.Connected)
            Dispose();
        
        byte[] dataCompressed = GZip.Compress(message, _packetId++, ReplyId);

        await UnderSocket.SendAsync(dataCompressed, SocketFlags.None);
    }
}