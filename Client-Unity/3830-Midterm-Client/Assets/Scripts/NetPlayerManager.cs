using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System;

public struct Player
{
    short playerID;
    string playerName;
    GameObject playerObj;

    public Player(short consID, string consName,GameObject consObj)
    {
        playerID = consID;
        playerName = consName;
        playerObj = consObj;
    }
}

public class NetPlayerManager : MonoBehaviour
{
    public static Dictionary<short, Player> playerDList = new Dictionary<short, Player>();

    public static short localPlayerID;
    public static string localPlayerName;
    public static GameObject localPlayerObj;

    public static bool isFirstInitialized = false;
   

    public static void InitialPlayerList(ref string allPlayer)
    {
        if(allPlayer.Length > 4 && !isFirstInitialized)
        {
            string[] players = allPlayer.Split("#");
            Debug.Log("players0: " + players[0]);
            foreach (string str in players)
            {
                //Debug.Log(str);
            }

            localPlayerID = short.Parse(players[0].Substring(0, 4));
            Debug.Log("ID :" + localPlayerID);
            localPlayerName = players[0].Substring(4, players[0].Length - 4);
            Debug.Log("Name :" + localPlayerName);
            localPlayerObj = Instantiate(Resources.Load<GameObject>("Player"), Vector3.zero, Quaternion.identity);
            localPlayerObj.AddComponent<cube>();



            for (int i = 1; i < players.Length; i++)
            {
                short playerID = short.Parse(players[i].Substring(0, 4));
                playerDList.Add(playerID, new Player(playerID,
                    players[i].Substring(4, players[i].Length - 1),
                    Resources.Load<GameObject>("Player")));
            }

            short[] shorts = { 0, localPlayerID };
            byte[] loginMsg = new byte[4];
            Buffer.BlockCopy(shorts, 0, loginMsg, 0, 4);

            NetworkManager.clientUDPSocket.SendTo(loginMsg, NetworkManager.serverUDPEP);
            NetworkManager.isUDPSetup = true;
            isFirstInitialized = true;
        }
        
    }

    public static void AddPlayer(ref short pID, ref string pName)
    {
        playerDList.Add(pID, new Player(pID, pName, Resources.Load<GameObject>("Player")));
    }

    public static void UpdatePlayer(ref short pID, ref float[] pPos)
    {
        
    }
}
