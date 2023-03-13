﻿using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Linq.Expressions;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Security.Cryptography;

public struct Player
{
    public CancellationTokenSource playerCTS;
    public short playerID { get; set; }
    public string playerName {  get; set; }
    public Socket playerTCPSocket { get; set; }
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


    public static int Main(String[] args)
    {
        StartServer();
        return 0;
    }

    static void StartServer()
    {
        IPAddress serverIP = IPAddress.Parse("192.168.2.43");
        IPEndPoint serverEP = new IPEndPoint(serverIP, 12581);

        serverTCP = new Socket(serverIP.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        serverUDP = new UdpClient(serverEP);

        try
        {
            serverTCP.Bind(serverEP);
            serverTCP.Listen(10);

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

                Console.WriteLine("");
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

                        string chatPiece = "[" + playerDList[pID].playerName + " - " + DateTime.Now.ToString("MM/dd hh:mm:ss tt") + "]: " + content;
                        chatList.Add(chatPiece);

                        byte[] byPiece = Encoding.ASCII.GetBytes(chatPiece);
                        short[] byHeader = { 1 };
                        byte[] msgToSend = new byte[2 + byPiece.Length];

                        Buffer.BlockCopy(byHeader, 0, msgToSend, 0, 2);
                        Buffer.BlockCopy(byPiece, 0, msgToSend, 2, byPiece.Length);

                        foreach(Player player in playerDList.Values)
                        {
                            if(player.playerID != pID)
                            {
                                player.playerTCPSocket.Send(msgToSend);
                            }
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

    }
}