using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Multiplay;
using Unity.Services.Matchmaker;
using Unity.Services.Matchmaker.Models;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.VisualScripting;
using System;



public class TestLobby : MonoBehaviour
{
    private Lobby hostLobby;
    private Lobby joinedLobby;
    float heartbeatTimer = 15f;
    float lobbyPollTimer = 1f;
    private string playerName;
    private const string KEY_START_GAME = "StartGame_RelayCode";
    [SerializeField] TestRelay tR;
    private string backfillTicketId;
    float backfillTimer;
    float backfillTimerMax = 1.1f;


#if DEDICATED_SERVER
    private float allocateTimer = 99999f;
    private bool alreadyAutoAllocated;
    static private IServerQueryHandler serverQueryHandler;
    PayloadAllocation payloadAllocation;
#endif

    private async void Start()
    {
        await UnityServices.InitializeAsync();

        
            InitializationOptions options = new InitializationOptions();
            options.SetProfile(UnityEngine.Random.Range(0, 10000).ToString());
            //AuthenticationService.Instance.SignedIn += () =>
            //{
            //    Debug.Log("Singed in " + AuthenticationService.Instance.PlayerId);
            //    playerName = "Player" + UnityEngine.Random.Range(0, 100);
            //    Debug.Log("Name: " + playerName);
            //};

#if !DEDICATED_SERVER
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
#endif
            await UnityServices.InitializeAsync(options);
#if DEDICATED_SERVER
            
            Debug.Log("DEDICATED_SERVER LOBBY");
            serverQueryHandler = await MultiplayService.Instance.StartServerQueryHandlerAsync(3, "ServerName", "Game", "1", "Standatrd");

            var serverConfig = MultiplayService.Instance.ServerConfig;
            if (serverConfig.AllocationId != "")
            {
                MultiplayEventCallbacks_Allocate(new MultiplayAllocation("", serverConfig.ServerId, serverConfig.AllocationId));
            }

            await MultiplayService.Instance.ReadyServerForPlayersAsync();
#endif
        
    }

    private void Update()
    {
        HandleLobbyHeartbeat();
        HandleLobbyList();
#if DEDICATED_SERVER
        allocateTimer -= Time.deltaTime;
        if(allocateTimer <= 0f)
        {
            allocateTimer = 999f;
            MultiplayEventCallbacks_Allocate(null);
        }

        if(serverQueryHandler != null)
        {
            if (NetworkManager.Singleton.IsServer)
            {
               serverQueryHandler.CurrentPlayers = (ushort)NetworkManager.Singleton.ConnectedClientsIds.Count;
            }
            serverQueryHandler.UpdateServerCheck();
        }

        if (backfillTicketId != null)
        {
            backfillTimer -= Time.deltaTime;
            if (backfillTimer <=0f)
            {
                backfillTimer = backfillTimerMax;
                HandleBackfillTickets();
            }
        }
#endif
    }

    private async void HandleLobbyList()
    {
        
        if (joinedLobby != null)
        {
            lobbyPollTimer -= Time.deltaTime;
            if (lobbyPollTimer < 0f)
            {
                lobbyPollTimer = 1.1f;

                Lobby lobby = await LobbyService.Instance.GetLobbyAsync(joinedLobby.Id);
                joinedLobby = lobby;
                PrintPlayers(joinedLobby);
            }

            if (joinedLobby.Data[KEY_START_GAME].Value != "0")
            {
                if (!IsLobbyHost())
                {
                    tR.JoinRelay(joinedLobby.Data[KEY_START_GAME].Value);
                }
                joinedLobby = null;
            }
        }       
    }

    async void HandleLobbyHeartbeat()
    {
        if(hostLobby != null)
        {
            heartbeatTimer -= Time.deltaTime;
            if(heartbeatTimer < 0f)
            {
                heartbeatTimer = 15f;

                await LobbyService.Instance.SendHeartbeatPingAsync(hostLobby.Id);
            }
        }
    }

    public async void CreateLobby()
    {
        try
        {
            string lobbyName = "Mylobby";
            int maxPlayers = 4;
            Unity.Services.Lobbies.Models.Player player = new Unity.Services.Lobbies.Models.Player
            {
                Data = new Dictionary<string, PlayerDataObject>
                {
                    { "PlayerName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, playerName) },
                }
            };
            CreateLobbyOptions options = new CreateLobbyOptions
            {
                IsPrivate = false,
                Player = player,
                Data = new Dictionary<string, DataObject>{
                    {KEY_START_GAME, new DataObject(DataObject.VisibilityOptions.Member, "0") }
                }
                
            };

            Lobby lobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, options);

            hostLobby = lobby;
            joinedLobby = hostLobby;

            PrintPlayers(hostLobby);
            Debug.Log("Created Lobby! " + lobbyName + " for: " + lobby.MaxPlayers + " " + lobby.Id + " " + lobby.LobbyCode);
            GameObject.Find("NetworkManagerUI").GetComponent<NetworkManagerUI>().ShowCode("Lobby Code: " + lobby.LobbyCode);
        }
        catch(LobbyServiceException ex)
        {
            Debug.Log(ex.Message);
        }
    }

    public async void ListLobbies()
    {
        try
        {
            QueryResponse queryResponse = await Lobbies.Instance.QueryLobbiesAsync();

            Debug.Log("Lobbies found: " + queryResponse.Results.Count);

            NetworkManagerUI nmUI = GameObject.Find("NetworkManagerUI").GetComponent<NetworkManagerUI>();
            string result = "Lobbies found: \n";
            int i = 1;
            foreach (Lobby lobby in queryResponse.Results)
            {
                result += i+ ". " + lobby.Name + " " + lobby.MaxPlayers + "\n";
                Debug.Log(lobby.Name + " " + lobby.MaxPlayers);
            }
            nmUI.ShowPlayers(result);
        }
        catch (LobbyServiceException ex)
        {
            Debug.Log(ex.Message);
        }
    }

    public async void QuickJoinLobby()
    {
        try
        {
            QuickJoinLobbyOptions options = new QuickJoinLobbyOptions()
            {
                Player = new Unity.Services.Lobbies.Models.Player
                {
                    Data = new Dictionary<string, PlayerDataObject>
                    {
                        {"PlayerName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, playerName) }
                    }
                }
            };
            Lobby lobby = await Lobbies.Instance.QuickJoinLobbyAsync(options);
            joinedLobby = lobby;
            GameObject.Find("NetworkManagerUI").GetComponent<NetworkManagerUI>().ShowCode("In Lobby with code: " + lobby.LobbyCode);
            PrintPlayers(joinedLobby);

        }
        catch (LobbyServiceException ex)
        {
            Debug.Log(ex.Message);
        }
    }

    public async void JoinLobby(string lobbyCode)
    {
        try
        {
            JoinLobbyByCodeOptions options = new JoinLobbyByCodeOptions()
            {
                Player = new Unity.Services.Lobbies.Models.Player
                {
                    Data = new Dictionary<string, PlayerDataObject>
                    {
                        {"PlayerName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, playerName) }
                    }
                }
            };
            Lobby lobby = await Lobbies.Instance.JoinLobbyByCodeAsync(lobbyCode, options);
            joinedLobby = lobby;

            Debug.Log("You have Joined a lobby with code: " + lobbyCode);
            GameObject.Find("NetworkManagerUI").GetComponent<NetworkManagerUI>().ShowCode("In Lobby with Code: " + lobbyCode);
            PrintPlayers(lobby);
        }
        catch (LobbyServiceException ex)
        {
            Debug.Log(ex.Message);
        }
    }

    public void PrintPlayers(Lobby lobby)
    {
#if DEDICATED_SERVER
        HandleUpdateBackfillTickets();
#endif
        Debug.Log("Players in Lobby: " + lobby.Name);
        int i = 1;
        NetworkManagerUI nmUI = GameObject.Find("NetworkManagerUI").GetComponent<NetworkManagerUI>();
        string result = "Players in Lobby: \n";
        foreach (Unity.Services.Lobbies.Models.Player player in lobby.Players)
        {
            result += "Player " + i + ": " + player.Data["PlayerName"].Value + "\n";
            Debug.Log(player.Id + " " + player.Data["PlayerName"].Value);
            i++;
        }
        nmUI.ShowPlayers(result);
    }

    public void LeaveLobby()
    {
        try
        {
            LobbyService.Instance.RemovePlayerAsync(joinedLobby.Id, AuthenticationService.Instance.PlayerId);
            GameObject.Find("NetworkManagerUI").GetComponent<NetworkManagerUI>().ShowPlayers("");
            GameObject.Find("NetworkManagerUI").GetComponent<NetworkManagerUI>().ShowCode("Lobby Code: ");
            hostLobby = null;
            joinedLobby = null;
        }
        catch(LobbyServiceException e) { Debug.Log(e); }
    }

    public async void StartGame()
    {
        if (IsLobbyHost())
        {
            try
            {
                Debug.Log("StartGame");

                string relayCode = await tR.CreateRelay();

                Lobby lobby = await Lobbies.Instance.UpdateLobbyAsync(joinedLobby.Id, new UpdateLobbyOptions
                {
                    Data = new Dictionary<string, DataObject>{
                        { KEY_START_GAME, new DataObject(DataObject.VisibilityOptions.Member, relayCode) }
                    }
                });

                await MatchmakerService.Instance.DeleteBackfillTicketAsync(backfillTicketId);

                joinedLobby = lobby;
            }
            catch (LobbyServiceException e) { Debug.Log(e); }
        }
    }

    public void ConnectToServer()
    {
        string ipv4Address = "34.90.195.170";
        ushort port = ushort.Parse("9000");
        
        NetworkManager.Singleton.GetComponent<UnityTransport>().SetConnectionData(ipv4Address, port);

        //NetworkManager.Singleton.StartClient();
    }

    bool IsLobbyHost()
    {
       return joinedLobby != null && joinedLobby.HostId == AuthenticationService.Instance.PlayerId;
    }

#if DEDICATED_SERVER
    private void MultiplayEventCallbacks_Allocate(MultiplayAllocation obj)
    {
        if (alreadyAutoAllocated)
        {
            return;
        }

        alreadyAutoAllocated = true;

        var serverConfig = MultiplayService.Instance.ServerConfig;

        string ipv4Address = "0.0.0.0";
        ushort port = serverConfig.Port;
        NetworkManager.Singleton.GetComponent<UnityTransport>().SetConnectionData(ipv4Address, port, "0.0.0.0");

        SetupBackfillTickets();

        NetworkManager.Singleton.StartServer();
    }

    async void SetupBackfillTickets()
    {
        PayloadAllocation payload = await MultiplayService.Instance.GetPayloadAllocationFromJsonAs<PayloadAllocation>();

        backfillTicketId = payload.BackfillTicketId;
        payloadAllocation = payload;

        backfillTimer = backfillTimerMax;
    }

    private async void HandleUpdateBackfillTickets()
    {
        if(backfillTicketId != null && payloadAllocation != null)
        {
            List<Unity.Services.Matchmaker.Models.Player> playerList = new List<Unity.Services.Matchmaker.Models.Player>();

            foreach(Unity.Services.Lobbies.Models.Player playerData in joinedLobby.Players)
            {
                playerList.Add(new Unity.Services.Matchmaker.Models.Player(playerData.Id.ToString()));
            }

            MatchProperties matchProperties = new MatchProperties(
                payloadAllocation.MatchProperties.Teams,
                playerList,
                payloadAllocation.MatchProperties.Region,
                payloadAllocation.MatchProperties.BackfillTicketId
            );

            try
            {
                await MatchmakerService.Instance.UpdateBackfillTicketAsync(payloadAllocation.BackfillTicketId,
                    new BackfillTicket(backfillTicketId, properties: new BackfillTicketProperties(matchProperties))
                );
            } 
            catch (MatchmakerServiceException e)
            {
                Debug.Log("ERROR: " + e);
            }
        }
    }

    async void HandleBackfillTickets()
    {
        BackfillTicket backfillTicket = await MatchmakerService.Instance.ApproveBackfillTicketAsync(backfillTicketId);
        backfillTicketId = backfillTicket.Id;
    }

    [Serializable]
    public class PayloadAllocation
    {
        public Unity.Services.Matchmaker.Models.MatchProperties MatchProperties;
        public string GeneratorName;
        public string QueueName;
        public string PoolName;
        public string EnviromentId;
        public string BackfillTicketId;
        public string MatchId;
        public string PoolId;
    }
#endif
}
