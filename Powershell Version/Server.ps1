# Setup listener on port 8080
$listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Any, 8080)
$listener.Start()
Write-Host "Server is listening on port 8080..."

# List to keep track of clients
$clients = @()

# Accept clients in a background job
$acceptClientsJob = Start-Job -ScriptBlock {
    param($listener, $clients)
    while ($true) {
        $client = $listener.AcceptTcpClient()
        $stream = $client.GetStream()
        $remoteEndPoint = $client.Client.RemoteEndPoint.ToString()
        $clientInfo = [PSCustomObject]@{
            Client = $client
            Stream = $stream
            Id = [Guid]::NewGuid().ToString()
            IpAddress = $remoteEndPoint
        }
        $using:clients += $clientInfo
        Write-Host "Client connected: $($clientInfo.Id) at $($clientInfo.IpAddress)"
    }
} -ArgumentList $listener, $clients

# Function to handle client commands
function HandleClient {
    param([System.Net.Sockets.TcpClient]$client)
    $stream = $client.GetStream()
    $reader = New-Object System.IO.StreamReader($stream)
    try {
        while ($client.Connected) {
            $line = $reader.ReadLine()
            if ($line -eq "ping") {
                Write-Host "Ping received, sending pong..."
                SendData $client "pong"
            } else {
                Write-Host "Command from $($client.Client.RemoteEndPoint): $line"
            }
        }
    } catch {
        Write-Host "Lost connection: $($_.Exception.Message)"
    } finally {
        $reader.Close()
        $stream.Close()
        $client.Close()
        $global:clients = $global:clients | Where-Object { $_ -ne $client }
        Write-Host "Client disconnected"
    }
}

# Function to send data to a client
function SendData {
    param([System.Net.Sockets.TcpClient]$client, [string]$data)
    $writer = New-Object System.IO.StreamWriter($client.GetStream())
    $writer.WriteLine($data)
    $writer.Flush()
}

# Listen to client data in the background
$handleClientsJob = Start-Job -ScriptBlock {
    param($clients)
    foreach ($client in $clients) {
        HandleClient $client.Client
    }
} -ArgumentList $clients

# User interface to interact with clients
while ($true) {
    $key = $host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    switch ($key.VirtualKeyCode) {
        { $_ -in 38, 40 } { # Up or Down arrow
            Write-Host "Client selection: $($key.VirtualKeyCode)"
        }
        13 { # Enter key
            $selectedClient = $clients | Select-Object -First 1
            if ($selectedClient) {
                Write-Host "Interacting with client: $($selectedClient.Id)"
                SendData $selectedClient.Client "Hello from server"
            } else {
                Write-Host "No client selected."
            }
        }
    }
}

# Ensure resources are cleaned up on exit
$listener.Stop()
Remove-Job -Job $acceptClientsJob -Force
Remove-Job -Job $handleClientsJob -Force
