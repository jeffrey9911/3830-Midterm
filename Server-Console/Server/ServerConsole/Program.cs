using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Linq.Expressions;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Reflection.Metadata;

public struct Player
{
    public CancellationTokenSource playerCTS;
    public short playerID { get; set; }
    public string playerName {  get; set; }
    public Socket playerTCPSocket { get; set; }
    public IPEndPoint playerEndPoint { get; set; }
    public float[] playerPosition { get; set; }

    public Player(Socket consSocket, short consID, string consName)
    {
        playerCTS = new CancellationTokenSource();
        playerID = consID;
        playerTCPSocket = consSocket;
        playerName = consName;
        playerPosition = new float[3];
    }

    public Player(CancellationTokenSource consCTS, Socket consSocket, IPEndPoint consEP, short consID, string consName)
    {
        playerCTS = consCTS;
        playerEndPoint = consEP;
        playerID = consID;
        playerTCPSocket = consSocket;
        playerName = consName;
        playerPosition = new float[3];
    }

}

public class ServerConsole
{
    private static CancellationTokenSource mainCts = new CancellationTokenSource();

    private static Socket serverTCP;
    private static UdpClient serverUDP;

    public static Dictionary<short, Player> playerDList = new Dictionary<short, Player>();
    public static List<string> chatList = new List<string>();

    static Random random = new Random();

    static void PrintPlayerList()
    {
        DateTime previousTime = DateTime.Now;

        double timer = 0.0;
        double interval = 1.0;

        while (true)
        {
            DateTime currentTime = DateTime.Now;

            double deltaTime = (currentTime - previousTime).TotalSeconds;
            timer += deltaTime;

            if (timer >= interval)
            {
                if(playerDList.Count > 0)
                {
                    Console.WriteLine("===========[Server Player List]===========");
                    foreach (Player player in playerDList.Values)
                    {
                        
                        Console.WriteLine("ID: {0}, Name: {1}", player.playerID, player.playerName);
                        Console.WriteLine("POS: {0}, {1}, {2}", player.playerPosition[0], player.playerPosition[1], player.playerPosition[2]);
                        
                    }
                    Console.WriteLine("==========================================");
                }
                timer -= interval;
            }


            previousTime = currentTime;
        }

    }

    public static int Main(String[] args)
    {
        Console.WriteLine("[Take Input]: Please input server local IP:");
        string ipAddress = Console.ReadLine();

        Console.WriteLine("[Take Input]: Please input server local TCP Port: (UDP port will be set to TCP Port + 1)");
        int portNumber = int.Parse(Console.ReadLine());

        StartServer(ipAddress, portNumber);
        Console.CancelKeyPress += new ConsoleCancelEventHandler(OnCancelKeyPress);

        Task.Run(() => { PrintPlayerList(); }, mainCts.Token);

        Console.WriteLine("[System Msg]: Server has started, press Ctrl+C or close the console window to quit.");
        Console.ReadLine();

        return 0;
    }

    static void StartServer(string ipA, int portN)
    {
        IPAddress serverIP = IPAddress.Parse(ipA);
        IPEndPoint serverTCPEP = new IPEndPoint(serverIP, portN);
        IPEndPoint serverUDPEP = new IPEndPoint(serverIP, portN + 1);

        serverTCP = new Socket(serverIP.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        serverUDP = new UdpClient(serverUDPEP);

        try
        {
            serverTCP.Bind(serverTCPEP);
            serverTCP.Listen(10);


            serverUDP.BeginReceive(ServerUDPReceiveCallBack, null);

            Task.Run(() => { TCPAccept(); }, mainCts.Token);
        }
        catch (Exception ex)
        {

            throw;
        }
    }

    /// <summary>
    /// TCP Accept thread, recurrsive, keep accepting
    /// </summary>
    static void TCPAccept()
    {
        Console.WriteLine("[System TCP]: Accepting new players");
        try
        {
            Socket acceptedClientSocket = serverTCP.Accept();
            Task.Run(() => { PlayerSetup(acceptedClientSocket); }, mainCts.Token);

        }
        catch (Exception ex)
        {

        }

        TCPAccept();    
    }

    /// <summary>
    /// Receives first TCP packet with header0. perfrom player login. Not recurrsive
    /// </summary>
    /// <param name="pSocket"> accepted socket from the player</param>
    static void PlayerSetup(Socket pSocket)
    {
        try
        {
            byte[] recvBuffer = new byte[1024];
            int recv = pSocket.Receive(recvBuffer);

            short header = GetHeader(recvBuffer, 0);

            if (header == 0)
            {
                string name = Encoding.ASCII.GetString(GetContent(recvBuffer.Take(recv).ToArray(), 2));

                short id = (short)random.Next(1000, 9999);

                playerDList.Add(id, new Player(pSocket, id, name));

                Console.WriteLine("[System TCP]: Player Created: {0}: {1}", id, name);

                if (playerDList.ContainsKey(id))
                {
                    string playerList = id.ToString() + name;

                    foreach(Player player in playerDList.Values)
                    {
                        if(player.playerID != id)
                        {
                            playerList += "#" + player.playerID.ToString() + player.playerName;
                        }
                        
                    }

                    byte[] allPlayer = Encoding.ASCII.GetBytes(playerList);

                    pSocket.Send(AddHeader(allPlayer, 0));

                    foreach (Player player in playerDList.Values)
                    {
                        if(player.playerID != id)
                        {
                            
                            player.playerTCPSocket.Send(AddHeader(AddHeader(Encoding.ASCII.GetBytes(name), id), 9));
                        }
                    }


                    Task.Run(() => { PlayerTCPReceive(id); }, playerDList[id].playerCTS.Token);
                    //PlayerTCPReceive(id);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
            throw;
        }
    }

    /// <summary>
    /// TCP receive from specific player; Receive content handling
    /// </summary>
    /// <param name="pName"></param>
    static void PlayerTCPReceive(short pID)
    {
        try
        {
            int nullMessageCount = 0;
            while(playerDList[pID].playerTCPSocket.Connected)
            {
                Console.WriteLine("[System Warning]: Invalid message received: " + nullMessageCount);
                if (playerDList.ContainsKey(pID))
                {

                    byte[] recvBuffer = new byte[1024];
                    int recv = playerDList[pID].playerTCPSocket.Receive(recvBuffer);

                    short[] headerBuffer = new short[2];
                    Buffer.BlockCopy(recvBuffer, 0, headerBuffer, 0, 4);


                    switch (headerBuffer[0])
                    {
                        // Chat
                        case 1:

                            string content = Encoding.ASCII.GetString(recvBuffer, 4, recv - 4);

                            string chatPiece = $"[<{DateTime.Now.ToString("MM/dd hh:mm:ss tt")}> {playerDList[pID].playerName}]: {content}";

                            Console.WriteLine(chatPiece);

                            chatList.Add(chatPiece);

                            byte[] pieceMsg = AddHeader(Encoding.ASCII.GetBytes(chatPiece), 1);


                            foreach (Player player in playerDList.Values)
                            {
                                player.playerTCPSocket.Send(pieceMsg);
                            }

                            break;


                        default:
                            nullMessageCount++;

                            if(nullMessageCount >= 100)
                            {
                                Console.WriteLine("[System Warning]: Invalid player. Perform player disconnection!");
                                DisconnectPlayer(pID);
                            }
                            break;
                    }
                }
            }


        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
            DisconnectPlayer(pID );
            throw;
        }

    }

    static void DisconnectPlayer(short pID)
    {
        if(playerDList.ContainsKey(pID))
        {
            
            playerDList[pID].playerTCPSocket.Close();
            playerDList[pID].playerCTS.Cancel();
            playerDList.Remove(pID);

            byte[] quitMsg = new byte[4];
            short[] shorts = { 999, pID };
            Buffer.BlockCopy(shorts, 0, quitMsg, 0, 4);
            foreach (Player player in playerDList.Values)
            {
                player.playerTCPSocket.Send(quitMsg);
            }

            Console.WriteLine("[System Warning]: Player{0} - {1} has been removed from the server", playerDList[pID].playerID, playerDList[pID].playerName);
        }
        else
        {
            Console.WriteLine("[System Warning]: Player doesn't exist, nothing is removed");
        }
        
    }


    static void ServerUDPReceiveCallBack(IAsyncResult result)
    {
        //UdpClient udpClient = (UdpClient)result.AsyncState;
        IPEndPoint clientEP = new IPEndPoint(IPAddress.Any, 0);
        byte[] recvBuffer = serverUDP.EndReceive(result, ref clientEP);


        switch (GetHeader(recvBuffer, 0))
        {
            case 0:
                short pid = GetHeader(recvBuffer, 2);

                if (playerDList.ContainsKey(pid))
                {
                    Player setupPlayer = new Player(playerDList[pid].playerCTS, playerDList[pid].playerTCPSocket, clientEP, playerDList[pid].playerID, playerDList[pid].playerName);

                    playerDList[pid] = setupPlayer;

                    Console.WriteLine("[System UDP]: Endpoint setup: " + playerDList[pid].playerEndPoint.Address + " " + playerDList[pid].playerEndPoint.Port);

                }
                break;

            case 1:
                short playerid = GetHeader(recvBuffer, 2);
                if(playerDList.ContainsKey(playerid))
                {
                    Buffer.BlockCopy(GetContent(recvBuffer, 4), 0, playerDList[playerid].playerPosition, 0, 12);

                    //Console.WriteLine("GET Position: {0}: {1}, {2}, {3}", playerid,
                    //    playerDList[playerid].playerPosition[0],
                     //   playerDList[playerid].playerPosition[1],
                       // playerDList[playerid].playerPosition[2]);

                    foreach (Player player in playerDList.Values)
                    {
                        if(player.playerID != playerid)
                        {
                            if(player.playerEndPoint != null)
                            {
                                serverUDP.Send(recvBuffer, recvBuffer.Length, player.playerEndPoint);
                            }
                            else
                            {
                                Console.WriteLine("[System UDP]: " + player.playerName + "'s endpoint is null");
                            }
                            
                        }
                    }

                }
                break;

            default:
                break;
        }
        serverUDP.BeginReceive(ServerUDPReceiveCallBack, null);
    }


    static short GetHeader(byte[] bytes, int offset)
    {
        short[] sheader = new short[1];
        Buffer.BlockCopy(bytes, offset, sheader, 0, 2);
        return sheader[0];
    }

    static byte[] AddHeader(byte[] bytes, short header)
    {
        byte[] buffer = new byte[bytes.Length + 2];
        short[] sBuffer = { header };
        Buffer.BlockCopy(sBuffer, 0, buffer, 0, 2);
        Buffer.BlockCopy(bytes, 0, buffer, 2, bytes.Length);
        return buffer;
    }

    static byte[] GetContent(byte[] buffer, int offset)
    {
        byte[] returnBy = new byte[buffer.Length - offset];
        Buffer.BlockCopy(buffer, offset, returnBy, 0, returnBy.Length);
        return returnBy;
    }

  

    static void OnCancelKeyPress(object sender, ConsoleCancelEventArgs args)
    {
        Console.WriteLine("[System Quit]: Server End Message");
        Thread.Sleep(1000);
        serverTCP.Close();
        serverUDP.Close();
        mainCts.Cancel();
    }
}