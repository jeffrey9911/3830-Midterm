using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System;

public struct Player
{
    public short playerID;
    public string playerName;
    public GameObject playerObj;

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


    //print
    float timerInterval = 1.0f;
    float timer;


    private void Update()
    {
        timer += Time.deltaTime;
        if(timer >= timerInterval)
        {
            if(false)
            {
                Debug.Log("===========================");
                foreach (Player player in playerDList.Values)
                {
                    Debug.Log(player.playerName);
                    Debug.Log(player.playerID);
                    Debug.Log(player.playerObj.transform.position);
                }
                Debug.Log("===========================");
            }
            timer -= timerInterval;
        }
    }

    public static void InitialPlayerList(ref string allPlayer)
    {
        if(allPlayer.Length > 4 && !isFirstInitialized)
        {
            string[] players = allPlayer.Split("#");
            

            localPlayerID = short.Parse(players[0].Substring(0, 4));
            Debug.Log("ID :" + localPlayerID);
            localPlayerName = players[0].Substring(4, players[0].Length - 4);
            Debug.Log("Name :" + localPlayerName);
            localPlayerObj = Instantiate(Resources.Load<GameObject>("Player"), Vector3.zero, Quaternion.identity);
            localPlayerObj.AddComponent<cube>();



            for (int i = 1; i < players.Length; i++)
            {
                Debug.Log(players[i] + "CReating");
                short playerID = short.Parse(players[i].Substring(0, 4));
                Debug.Log("CReating ID: " + playerID);
                playerDList.Add(playerID, new Player(playerID, players[i].Substring(4, players[i].Length - 4),
                    Instantiate(Resources.Load<GameObject>("Player"))  ));
                Debug.Log("CReating Name: " + playerDList[playerID].playerName);
            }

            short[] shorts = { 0, localPlayerID };
            byte[] loginMsg = new byte[4];
            Buffer.BlockCopy(shorts, 0, loginMsg, 0, 4);

            NetworkManager.clientUDPSocket.SendTo(loginMsg, NetworkManager.serverUDPEP);
            NetworkManager.isUDPSetup = true;
            NetworkManager.isReceiveUDP = true;
            isFirstInitialized = true;
        }
        
    }

    public static void AddPlayer(ref short pID, ref string pName)
    {
        playerDList.Add(pID, new Player(pID, pName, Instantiate(Resources.Load<GameObject>("Player"))));
    }

    public static void UpdatePlayer(ref short pID, ref float[] pPos)
    {
        if(playerDList.ContainsKey(pID))
        {
            playerDList[pID].playerObj.transform.position = new Vector3(pPos[0], pPos[1], pPos[2]);
        }
    }
}
