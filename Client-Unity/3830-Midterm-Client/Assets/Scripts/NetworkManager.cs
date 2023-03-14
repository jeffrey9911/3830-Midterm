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

public class NetworkManager : MonoBehaviour
{
    public TMP_InputField _inp_playername;
    public TMP_InputField _inp_ip;
    public TMP_InputField _inp_port;

    public TMP_InputField _inp_message;


    public GameObject _panelLogin;

    private static System.Random rand = new System.Random();

    private static CancellationTokenSource cts;
    private static short localPlayerID;
    private string localPlayerName;

    private static IPEndPoint serverEP;
    private static Socket clientTCPSocket;
    private static Socket clientUDPSocket;

    public void LoginToServer()
    {
        //IPAddress ip = IPAddress.Parse(_inp_ip.text);
        IPAddress ip = Dns.GetHostAddresses("jeffrey9911.ddns.net")[0];
        serverEP = new IPEndPoint(ip, 8888/*int.Parse(_inp_port.text)*/);

        clientTCPSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        clientUDPSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

        cts = new CancellationTokenSource();

        Task.Run(() => { ClientLogin(_inp_playername.text); }, cts.Token);

        _panelLogin.SetActive(false);
    }

    static void ClientLogin(string playername)
    {
        try
        {
            clientTCPSocket.Connect(serverEP);
            // Connected

            short localPlayerID = (short)rand.Next(1000, 9999);
            short[] headerBuffer = { 0, localPlayerID };
            byte[] pName = Encoding.ASCII.GetBytes(playername);
            byte[] loginMsg = new byte[headerBuffer.Length * 2 + pName.Length];

            Buffer.BlockCopy(headerBuffer, 0, loginMsg, 0, 4);
            Buffer.BlockCopy(pName, 0, loginMsg, 4, pName.Length);

            clientTCPSocket.Send(loginMsg);
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
        short[] shorts = { 1, localPlayerID };
        byte[] text = Encoding.ASCII.GetBytes(_inp_message.text);
        byte[] msg = new byte[text.Length + 4];
        Buffer.BlockCopy(shorts, 0, msg, 0, 4);
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
