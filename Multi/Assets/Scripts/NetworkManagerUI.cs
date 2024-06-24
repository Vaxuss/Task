using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Unity.Services.Lobbies;
using Unity.Services.Matchmaker.Models;
using Unity.Services.Matchmaker;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Netcode.Transports.UTP;

public class NetworkManagerUI : MonoBehaviour
{
    [SerializeField] private Button serverBtn;
    [SerializeField] private Button quickPlay;
    [SerializeField] private Button hostBtn;
    [SerializeField] private Button clientBtn;
    [SerializeField] private Button lobbyBtn;
    [SerializeField] private Button joinLobbyCodeBtn;
    [SerializeField] private Button joinLobbyBtn;
    [SerializeField] private Button createLobbyBtn;
    [SerializeField] private Button leaveLobbyBtn;
    [SerializeField] private Button startBtn;
    [SerializeField] private GameObject code;
    [SerializeField] private TestLobby tL;
    [SerializeField] private GameObject tmp;
    [SerializeField] private GameObject lobbyList;
    CreateTicketResponse createTicketResponse;
    float poolTicketTimer;
    float poolTicketTimerMax = 1.1f;


    private void Awake()
    {
        serverBtn.onClick.AddListener(() =>
        {
            tL.ConnectToServer();
        });
        hostBtn.onClick.AddListener(() =>
        {
            NetworkManager.Singleton.StartHost();
        });
        clientBtn.onClick.AddListener(() =>
        {
            NetworkManager.Singleton.StartClient();
        });
        createLobbyBtn.onClick.AddListener(() =>
        {
            tL.CreateLobby();
        });
        lobbyBtn.onClick.AddListener(() =>
        {
            tL.ListLobbies();
        });
        joinLobbyCodeBtn.onClick.AddListener(() =>
        {
            tL.JoinLobby(code.GetComponent<TMP_InputField>().text);
        });
        joinLobbyBtn.onClick.AddListener(() =>
        {
            tL.QuickJoinLobby();
        });
        leaveLobbyBtn.onClick.AddListener(() =>
        {
            tL.LeaveLobby();
        });
        startBtn.onClick.AddListener(() =>
        {
            tL.StartGame();
        });
        quickPlay.onClick.AddListener(() =>
        {
            FindMatch();
        });
    }

    async void FindMatch()
    {
        createTicketResponse = await MatchmakerService.Instance.CreateTicketAsync(new List<Unity.Services.Matchmaker.Models.Player> {
            new Unity.Services.Matchmaker.Models.Player(AuthenticationService.Instance.PlayerId)
        }, new CreateTicketOptions { QueueName = "TestQueue" });

        poolTicketTimer = poolTicketTimerMax;
    }

    public void ShowCode(string joinCode)
    {
        tmp.GetComponent<TMP_Text>().text = joinCode;
    }

    public void ShowPlayers(string lobbyListText)
    {
        lobbyList.GetComponent<TMP_Text>().text = lobbyListText;
    }

    private void Update()
    {
        if (createTicketResponse != null)
        {
            poolTicketTimer -= Time.deltaTime;
            if(poolTicketTimer <= 0f)
            {
                poolTicketTimer = 1.1f;
                PoolMatchmakerTicket();
            }
        }
    }

    private async void PoolMatchmakerTicket()
    {
        TicketStatusResponse ticketStatusResponse = await MatchmakerService.Instance.GetTicketAsync(createTicketResponse.Id);

        if(ticketStatusResponse == null)
        {
            return;
        }

        if(ticketStatusResponse.Type == typeof(MultiplayAssignment))
        {
            MultiplayAssignment multiplayAssignment = ticketStatusResponse.Value as MultiplayAssignment;

            switch(multiplayAssignment.Status)
            {
                case MultiplayAssignment.StatusOptions.Found:
                    createTicketResponse = null;

                    string ipv4Address = multiplayAssignment.Ip;
                    ushort port = (ushort)multiplayAssignment.Port;
                    NetworkManager.Singleton.GetComponent<UnityTransport>().SetConnectionData(ipv4Address, port);

                    NetworkManager.Singleton.StartClient();
                    break;
                case MultiplayAssignment.StatusOptions.InProgress:
                    break;
                case MultiplayAssignment.StatusOptions.Failed:
                    createTicketResponse = null;
                    lobbyList.GetComponent<TMP_Text>().text = "Server connection failed";
                    break;
                case MultiplayAssignment.StatusOptions.Timeout:
                    createTicketResponse = null;
                    lobbyList.GetComponent<TMP_Text>().text = "Timeout";
                    break;
            }
        }
    }
}
