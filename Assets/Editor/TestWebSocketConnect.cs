using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

public static class TestWebSocketConnect
{
    [MenuItem("Tools/Test WebSocket Connect to Local Server")]
    private static void MenuConnect()
    {
        // 必要に応じてここを編集してください（例: "ws://127.0.0.1:8080/" / "wss://127.0.0.1:8081/"）
        string url = "ws://127.0.0.1:8080/";

        Debug.Log($"[TestWebSocketConnect] attempting to connect to {url}");
        _ = ConnectAsync(url);
    }

    private static async Task ConnectAsync(string url)
    {
        using (var client = new ClientWebSocket())
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                await client.ConnectAsync(new Uri(url), cts.Token);
                Debug.Log($"[TestWebSocketConnect] connected: State={client.State}");
                await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TestWebSocketConnect] Exception: {ex.GetType().Name}: {ex.Message}\n{ex}");
            }
        }
    }
}