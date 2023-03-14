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
    public byte[] pEPiP = new byte[30];
    public int[] pEPpo = new int[1];
    public float[] playerPosition { get; set; }

    public Player(Socket consSocket, short consID, string consName)
    {
        playerCTS = new CancellationTokenSource();
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
                    Console.WriteLine("==========================================");
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

        StartServer();
        Console.CancelKeyPress += new ConsoleCancelEventHandler(OnCancelKeyPress);

        //Task.Run(() => { PrintPlayerList(); }, mainCts.Token);

        Console.WriteLine("Press Ctrl+C or close the console window to quit.");
        Console.ReadLine();

        return 0;
    }

    static void StartServer()
    {
        IPAddress serverIP = IPAddress.Parse("192.168.2.43");
        IPEndPoint serverTCPEP = new IPEndPoint(serverIP, 8888);
        IPEndPoint serverUDPEP = new IPEndPoint(serverIP, 8888);

        serverTCP = new Socket(serverIP.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        serverUDP = new UdpClient(serverUDPEP);

        try
        {
            serverTCP.Bind(serverTCPEP);
            serverTCP.Listen(10);

            Task.Run(() => { TCPAccept(); }, mainCts.Token);
            Task.Run(() => { ServerUDPReceive(); }, mainCts.Token);
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
        Console.WriteLine("");
        try
        {
            Socket acceptedClientSocket = serverTCP.Accept();
            PlayerSetup(acceptedClientSocket);

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

            short[] headerBuffer = new short[2];
            Buffer.BlockCopy(recvBuffer, 0, headerBuffer, 0, 4);

            if (headerBuffer[0] == 0)
            {
                while (playerDList.ContainsKey(headerBuffer[1]))
                {
                    headerBuffer[1] += 1;
                }

                string name = Encoding.ASCII.GetString(recvBuffer, 4, recv - 4);
                playerDList.Add(headerBuffer[1], new Player(pSocket, headerBuffer[1], name));


                if(playerDList.ContainsKey(headerBuffer[1]))
                {
                    Task.Run(() => { PlayerTCPReceive(headerBuffer[1]); }, playerDList[headerBuffer[1]].playerCTS.Token);
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
                        

                        string chatPiece = "[" + playerDList[pID].playerName + " - " + DateTime.Now.ToString("MM/dd hh:mm:ss tt") + "]: \n" + content;
                        chatList.Add(chatPiece);

                        byte[] byPiece = Encoding.ASCII.GetBytes(chatPiece);
                        short[] byHeader = { 1 };
                        byte[] msgToSend = new byte[2 + byPiece.Length];

                        Buffer.BlockCopy(byHeader, 0, msgToSend, 0, 2);
                        Buffer.BlockCopy(byPiece, 0, msgToSend, 2, byPiece.Length);

                        foreach(Player player in playerDList.Values)
                        {
                            player.playerTCPSocket.Send(msgToSend);
                        }

                        break;


                    default:
                        break;
                }
            }
        }
        catch(Exception ex)
        {
            Console.WriteLine(ex.ToString());
            playerDList[pID].playerCTS.Cancel();
            playerDList.Remove(pID);
            throw;
        }
        
        PlayerTCPReceive(pID);

    }

    static void ServerUDPReceive()
    {
        byte[] recvBuffer = new byte[1024];
        IPEndPoint clientEP = new IPEndPoint(IPAddress.Any, 0);
        recvBuffer = serverUDP.Receive(ref clientEP);

        

        switch(GetHeader(recvBuffer))
        {
            case 0:
                short pid = GetID(recvBuffer);

                if(playerDList.ContainsKey(pid))
                {
                    byte[] ip = Encoding.ASCII.GetBytes(clientEP.Address.ToString());
                    int[] port = { clientEP.Port };
                    Buffer.BlockCopy(ip, 0, playerDList[pid].pEPiP, 0, ip.Length);
                    Buffer.BlockCopy(port, 0, playerDList[pid].pEPpo, 0, 4);

                    Console.WriteLine("SETUP!: " + Encoding.ASCII.GetString(playerDList[pid].pEPiP) + " " + playerDList[pid].pEPpo[0]);

                    Buffer.BlockCopy(GetContent(recvBuffer), 0, playerDList[pid].playerPosition, 0, 12);

                    short[] header = { 0, -1 };

                    byte[] allTrans = new byte[2 + playerDList.Count * 14];
                    Buffer.BlockCopy(header, 0, allTrans, 0, 2);

                    int ind = 0;
                    foreach(Player player in playerDList.Values)
                    {
                        header[1] = player.playerID;
                        Buffer.BlockCopy(header, 2, allTrans, ind * 14, 2);
                        Buffer.BlockCopy(player.playerPosition, 0, allTrans, ind * 14 + 2, 12);
                        ind++;
                    }


                    foreach(Player player in playerDList.Values)
                    {
                        if(player.playerID != pid)
                        {
                            serverUDP.Send(allTrans, new IPEndPoint(IPAddress.Parse(Encoding.ASCII.GetString(player.pEPiP)), player.pEPpo[0]));
                        }
                        
                    }

                    //Console.WriteLine(allTrans.Length);

                    //Console.WriteLine(clientEP.Address.ToString() + " " + clientEP.Port.ToString());
                    
                    //playerDList[pid].playerTCPSocket.Send(allTrans);

                }
                break;

            default:
                break;
        }
        ServerUDPReceive();
    }

    static short GetHeader(byte[] header)
    {
        short[] sheader = new short[1];
        Buffer.BlockCopy(header, 0, sheader, 0, 2);
        return sheader[0];
    }

    static short GetID(byte[] header)
    {
        short[] sheader = new short[1];
        Buffer.BlockCopy(header, 2, sheader, 0, 2);
        return sheader[0];
    }

    static byte[] GetContent(byte[] buffer)
    {
        byte[] returnBy = new byte[buffer.Length - 4];
        Buffer.BlockCopy(buffer, 4, returnBy, 0, returnBy.Length);
        return returnBy;
    }

    static void OnCancelKeyPress(object sender, ConsoleCancelEventArgs args)
    {
        Console.WriteLine("Quitting...");
        serverTCP.Close();
        serverUDP.Close();
        mainCts.Cancel();
    }
}