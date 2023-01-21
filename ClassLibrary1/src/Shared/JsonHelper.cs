using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace WsSharp.Handlers;

internal static class JsonHelper
{
    internal static bool TryDeserialize<TClass>(string? message, [NotNullWhen(true)] out TClass? result)
    {
        result = default;

        if (message is null)
            return false;

        try
        {
            result = JsonSerializer.Deserialize<TClass>(message)!;
            return true;
        }
        catch(Exception e)
        {
            return false;
        }
    }

    internal static bool TryDeserializeTo<TClass>(this string message, out TClass? result)
        => TryDeserialize(message, out result);
}