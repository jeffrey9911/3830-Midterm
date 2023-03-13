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

public class NetworkManager : MonoBehaviour
{
    public TMP_InputField _inp_playername;
    public TMP_InputField _inp_ip;
    public TMP_InputField _inp_port;

    public GameObject _panelLogin;

    private static System.Random rand = new System.Random();

    private static CancellationTokenSource cts;
    private short localPlayerID;
    private string localPlayerName;

    private static IPEndPoint serverEP;
    private static Socket clientTCPSocket;
    private static Socket clientUDPSocket;

    public void LoginToServer()
    {
        //IPAddress ip = IPAddress.Parse(_inp_ip.text);
        IPAddress ip = Dns.GetHostAddresses("jeffrey9911.ddns.net")[0];
        serverEP = new IPEndPoint(ip, int.Parse(_inp_port.text));

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

        }
        catch (Exception ex)
        {

        }
    }

    private void OnApplicationQuit()
    {
        cts.Cancel();
        clientTCPSocket.Close();
        clientUDPSocket.Close();
    }

}
