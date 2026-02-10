using System;
using System.Net.Sockets;
using UnityEditor;
using UnityEngine;

public static class TestTcpConnect
{
    [MenuItem("Tools/Test TCP Connect to Local Server")]
    private static void MenuConnect()
    {
        // 編集: 接続先をここに書く
        string host = "127.0.0.1";
        int port = 8080;

        Debug.Log($"[TestTcpConnect] attempting to connect to {host}:{port}");
        try
        {
            using (var client = new TcpClient())
            {
                var async = client.BeginConnect(host, port, null, null);
                bool success = async.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(3));
                if (!success)
                {
                    Debug.LogError($"[TestTcpConnect] connection timeout to {host}:{port}");
                    return;
                }
                client.EndConnect(async);
                Debug.Log($"[TestTcpConnect] connected to {host}:{port} (LocalEndPoint={client.Client.LocalEndPoint})");
                client.Close();
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TestTcpConnect] Exception: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
        }
    }
}