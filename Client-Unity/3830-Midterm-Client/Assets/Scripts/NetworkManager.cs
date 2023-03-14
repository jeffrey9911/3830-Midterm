using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using UnityEngine.SocialPlatforms;
using UnityEngine.UI;

using System.Threading.Tasks;

using TMPro;
using Unity.VisualScripting;
using System.Threading;
using UnityEngine.Analytics;
using UnityEngine.UIElements;

public class NetworkManager : MonoBehaviour
{
    public TMP_InputField _inp_playername;
    public TMP_InputField _inp_ip;
    public TMP_InputField _inp_port;

    public TMP_InputField _inp_message;

    public GameObject _panelLogin;

    public GameObject _player;

    private static System.Random rand = new System.Random();

    private static CancellationTokenSource cts;
    private static short localPlayerID;
    private string localPlayerName;

    private static IPEndPoint serverTCPEP;
    private static IPEndPoint serverUDPEP;
    private static Socket clientTCPSocket;
    private static Socket clientUDPSocket;
    private static UdpClient udpClient;

    static bool isReady = false;
    static bool isUDPSetup = false;

    float updateInterval = 1.0f;
    float timer = 0.0f;
    private void Update()
    {
        timer += Time.deltaTime;
        if(timer >= updateInterval && isReady)
        {
            ClientUDPPosition();
            timer -= updateInterval;
        }
    }

    public void LoginToServer()
    {
        //IPAddress ip = IPAddress.Parse("192.168.2.43"/*_inp_ip.text*/);
        IPAddress ip = Dns.GetHostAddresses("jeffrey9911.ddns.net")[0];
        serverTCPEP = new IPEndPoint(ip, 8888/*int.Parse(_inp_port.text)*/);
        serverUDPEP = new IPEndPoint(ip, 8888/*int.Parse(_inp_port.text)*/);
        //serverUDPEP = new IPEndPoint(IPAddress.Any, 0/*int.Parse(_inp_port.text)*/);

        clientTCPSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        clientUDPSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        udpClient = new UdpClient(0);

        cts = new CancellationTokenSource();

        Task.Run(() => { ClientLogin(_inp_playername.text); }, cts.Token);

        _panelLogin.SetActive(false);
    }

    static void ClientLogin(string playername)
    {
        try
        {
            clientTCPSocket.Connect(serverTCPEP);
            // Connected

            localPlayerID = (short)rand.Next(1000, 9999);
            short[] headerBuffer = { 0, localPlayerID };
            byte[] pName = Encoding.ASCII.GetBytes(playername);
            byte[] loginMsg = new byte[headerBuffer.Length * 2 + pName.Length];

            Buffer.BlockCopy(headerBuffer, 0, loginMsg, 0, 4);
            Buffer.BlockCopy(pName, 0, loginMsg, 4, pName.Length);

            clientTCPSocket.Send(loginMsg);
            isReady = true;
            //Task.Run(() => { ClientUDPReceive(); }, cts.Token);
            ClientTCPReceive();
        }
        catch (Exception ex)
        {

        }
    }

    static void ClientTCPReceive()
    {
        Debug.Log("TCP RECEIVE");

        try
        {
            byte[] recvBuffer = new byte[1024];
            int recv = clientTCPSocket.Receive(recvBuffer);

            Debug.Log(recv);


            switch (GetHeader(recvBuffer))
            {
                case 0:
                    break;

                case 1:
                    string newMsg = Encoding.ASCII.GetString(GetContent(recvBuffer));
                    UnityMainThreadDispatcher.Instance().Enqueue(() => CreateMessage(newMsg));
                    break;
                case 3:
                    
                    
                    break;
                default:
                    break;
            }
            ClientTCPReceive();
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            throw;
        }
        
    }

    void ClientUDPPosition()
    {
        float[] fPos =
        {
            _player.transform.position.x,
            _player.transform.position.y,
            _player.transform.position.z
        };

        byte[] byPosWH = new byte[fPos.Length * 4 + 4];
        Buffer.BlockCopy(CreateHeader(0), 0, byPosWH, 0, 4);
        Buffer.BlockCopy(fPos, 0, byPosWH, 4, 12);

        clientUDPSocket.SendTo(byPosWH, serverUDPEP);
        //clientUDP.Send(byPosWH, byPosWH.Length, serverUDPEP);
    }

    static void ClientUDPReceive()
    {
        Debug.Log("UDP RE");
        try
        {
            byte[] getBuffer = new byte[1024];
            IPEndPoint sEP = new IPEndPoint(IPAddress.Any, 0);
            getBuffer = udpClient.Receive(ref sEP);
            //int recv = clientUDPSocket.Receive(getBuffer);

            Debug.Log(GetHeader(getBuffer));
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            throw;
        }

        ClientUDPReceive();
    }

    static short GetHeader(byte[] header)
    {
        short[] sheader = new short[1];
        Buffer.BlockCopy(header, 0, sheader, 0, 2);
        return sheader[0];
    }

    static byte[] CreateHeader(short header)
    {
        short[] shorts = { header, localPlayerID };
        byte[] retBy = new byte[shorts.Length * 2];
        Buffer.BlockCopy(shorts, 0, retBy, 0, 4);
        return retBy;
    }

    static byte[] GetContent(byte[] buffer)
    {
        byte[] returnBy = new byte[buffer.Length - 2];
        Buffer.BlockCopy(buffer, 2, returnBy, 0, returnBy.Length);
        return returnBy;
    }

    public static void CreateMessage(string msg)
    {
        GameObject obj = Instantiate(Resources.Load<GameObject>("IMG_Message"), GameObject.Find("SCVContent").transform);
        obj.transform.Find("TXT_Message").GetComponent<TMP_Text>().text = msg;
    }

    public void SendMessage()
    {
        byte[] text = Encoding.ASCII.GetBytes(_inp_message.text);
        byte[] msg = new byte[text.Length + 4];
        Buffer.BlockCopy(CreateHeader(1), 0, msg, 0, 4);
        Buffer.BlockCopy(text, 0, msg, 4, text.Length);
        clientTCPSocket.Send(msg);
        _inp_message.text = "";
    }

    private void OnApplicationQuit()
    {
        cts.Cancel();
        clientTCPSocket.Close();
        clientUDPSocket.Close();
    }

}
