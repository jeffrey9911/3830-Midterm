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

    private static IPEndPoint serverTCPEP;
    public static IPEndPoint serverUDPEP;

    public static Socket clientTCPSocket;
    public static Socket clientUDPSocket;

    public static byte[] tcpReceiveBuffer = new byte[1024];
    public static byte[] tcpSendBuffer = new byte[1024];

    public static byte[] udpReceiveBuffer = new byte[1024];
    public static byte[] udpSendBuffer = new byte[1024];

    // Threads control
    private static Mutex mutex = new Mutex();
    public static List<Thread> threads = new List<Thread>();


    // FLAGS
    public static bool isLogin = false;
    public static bool isUDPSetup = false;
    public static bool isReceiveUDP = false;

    // TIMERS
    float updateInterval = 0.1f;
    float timer = 0.0f;

    public static bool test = false;

    private void Start()
    {
        
    }

    private void Update()
    {
        timer += Time.deltaTime;
        if(timer >= updateInterval && isUDPSetup)
        {
            ClientUDPPosition();
            timer -= updateInterval;
        }
    }

    public void LoginToServer()
    {
        IPAddress ip = IPAddress.Parse(_inp_ip.text);
        serverTCPEP = new IPEndPoint(ip, int.Parse(_inp_port.text));
        serverUDPEP = new IPEndPoint(ip, int.Parse(_inp_port.text) + 1);

        clientTCPSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        clientUDPSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        
        Thread thread = new Thread(() => ClientLogin(_inp_playername.text));
        threads.Add(thread);
        thread.Start();
        
        _panelLogin.SetActive(false);
    }

    static void ClientLogin(string playername)
    {
        try
        {
            clientTCPSocket.Connect(serverTCPEP);
            Debug.Log("TCP Connect");
            //clientUDPSocket.Bind(serverUDPEP);
            clientUDPSocket.Bind(new IPEndPoint(IPAddress.Any, 0));
            Debug.Log("UDP Bind");
            // Connected

            byte[] loginMsg = AddHeader(Encoding.ASCII.GetBytes(playername), 0);


            Debug.Log("Pname length: " + loginMsg.Length);

            clientTCPSocket.Send(loginMsg);
            Debug.Log("Login");
            isLogin = true;


            ClientTCPReceive();
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }

    static void ClientTCPReceive()
    {
        if(mutex.WaitOne())
        {
            Debug.Log("TCP RECEIVE");

            try
            {
                byte[] recvBuffer = new byte[2048];
                int recv = clientTCPSocket.Receive(recvBuffer);

                switch (GetHeader(recvBuffer, 0))
                {
                    case 0:
                        Debug.Log("TCP0");
                        if(!isUDPSetup)
                        {
                            string allPlayer = Encoding.ASCII.GetString(GetContent(recvBuffer, 2));
                            UnityMainThreadDispatcher.Instance().Enqueue(() => NetPlayerManager.InitialPlayerList(ref allPlayer));
                        }
                        else
                        {
                            Debug.Log("Initialize Ignored");
                        }
                        
                        
                        break;

                    case 1:
                        Debug.Log("TCP1");
                        foreach (byte b in recvBuffer)
                        {
                            Debug.Log(b);
                        }



                        UnityMainThreadDispatcher.Instance().Enqueue(() => CreateMessage(Encoding.ASCII.GetString(GetContent(recvBuffer, 2))));
                        break;
                    case 9:
                        Debug.Log("TCP9");
                        short pid = GetHeader(recvBuffer, 2);
                        Debug.Log("9: " + pid);

                        string newPlayerName = Encoding.ASCII.GetString(GetContent(recvBuffer, 4));
                        Debug.Log("9: " + newPlayerName);

                        UnityMainThreadDispatcher.Instance().Enqueue(() => NetPlayerManager.AddPlayer(ref pid, ref newPlayerName));

                        break;

                    case 999:
                        Debug.Log("TCP 999");
                        short quitID = GetHeader(recvBuffer, 2);

                        UnityMainThreadDispatcher.Instance().Enqueue(() => NetPlayerManager.DeletePlayer(ref quitID));

                        break;
                    default:
                        break;
                }
                
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                throw;
            }
        }

        ClientTCPReceive();
    }

    void ClientUDPPosition()
    {
        float[] fPos =
        {
            NetPlayerManager.localPlayerObj.transform.position.x,
            NetPlayerManager.localPlayerObj.transform.position.y,
            NetPlayerManager.localPlayerObj.transform.position.z
        };

        byte[] byPosWH = new byte[fPos.Length * 4 + 4];
        short[] shorts = { 1, NetPlayerManager.localPlayerID };
        Buffer.BlockCopy(shorts, 0, byPosWH, 0, 4);
        Buffer.BlockCopy(fPos, 0, byPosWH, 4, 12);

        clientUDPSocket.SendTo(byPosWH, serverUDPEP);

        if(isReceiveUDP)
        {
            Thread thread = new Thread(ClientUDPReceive);
            threads.Add(thread);
            thread.Start();
            isReceiveUDP = false;
        }
    }

    static void ClientUDPReceive()
    {
        Debug.Log("UDP RE");
        try
        {
            byte[] getBuffer = new byte[1024];
            IPEndPoint sEP = new IPEndPoint(IPAddress.Any, 0);
            int recv = clientUDPSocket.Receive(getBuffer);

            if(GetHeader(getBuffer, 0) == 1)
            {
                short pID = GetHeader(getBuffer, 2);
                float[] playerPos = new float[3];
                Buffer.BlockCopy(getBuffer, 4, playerPos, 0, 12);


                Debug.Log(recv + "POS RECEIVED: " + pID.ToString() + " " + playerPos[0] + " " + playerPos[1] + " " + playerPos[2]);

                UnityMainThreadDispatcher.Instance().Enqueue(() => NetPlayerManager.UpdatePlayer(ref pID, ref playerPos));
            }

            
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            throw;
        }

        ClientUDPReceive();
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

    public static void CreateMessage(string msg)
    {
        GameObject obj = Instantiate(Resources.Load<GameObject>("IMG_Message"), GameObject.Find("SCVContent").transform);
        obj.transform.Find("TXT_Message").GetComponent<TMP_Text>().text = msg;
    }

    public void SendMessage()
    {
        byte[] text = Encoding.ASCII.GetBytes(_inp_message.text);
        byte[] msg = new byte[text.Length + 4];
        short[] shorts = { 1, NetPlayerManager.localPlayerID };
        Buffer.BlockCopy(shorts, 0, msg, 0, 4);
        Buffer.BlockCopy(text, 0, msg, 4, text.Length);
        clientTCPSocket.Send(msg);
        _inp_message.text = "";
    }

    public void TestThread()
    {
        test = true;
        Thread.CurrentThread.Abort();
    }

    private void OnApplicationQuit()
    {
        foreach (Thread thread in threads)
        {
            thread.Abort();
        }
        clientTCPSocket.Close();
        clientUDPSocket.Close();
    }

}
