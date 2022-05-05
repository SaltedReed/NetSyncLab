using System.Collections.Generic;
using UnityEngine;
using Mirror;

namespace NetSyncLab.Lockstep
{

    public sealed class Server : NetworkBehaviour
    {
        private class ClientInfo
        {
            public int playerId = -1;
            public NetworkConnection conn;
        }


        [Header("Lockstep")]
        [SerializeField]
        private float m_frameDelta = 0.033f;

        public bool HasSetup { get; private set; }
        public bool IsRunning { get; private set; }

        public float FrameDelta
        {
            get => m_frameDelta;
            set => m_frameDelta = value;
        }

        public int CurTick { get; private set; }

        public int PlayerCount => m_allClients==null ? 0 : m_allClients.Count;

        private List<Msg_C2S_JoinRoom> m_joinRoomMsgs;
        private float m_lastTime;
        private Dictionary<int, List<PlayerInput>> m_inputs = new Dictionary<int, List<PlayerInput>>();
        private int m_nextPlayerId = 0;
        private List<ClientInfo> m_allClients = new List<ClientInfo>();


        #region Process Messages

        private void OnMsgJoinRoom(NetworkConnection conn, Msg_C2S_JoinRoom msg)
        {
            Debug.Log($"[server] tick {CurTick} | {msg}: conn hash={msg.connHash}");

            if (IsRunning)
            {
                Msg_S2C_JoinRoomResult resultMsg = new Msg_S2C_JoinRoomResult { result = 1 };
                conn.Send(resultMsg);

                return;
            }

            if (m_joinRoomMsgs.FindIndex((Msg_C2S_JoinRoom m) => { return m.connHash == msg.connHash; }) < 0)
            {
                m_joinRoomMsgs.Add(msg);

                ClientInfo ci = new ClientInfo 
                { 
                    playerId = m_nextPlayerId++,
                    conn = conn
                };
                m_allClients.Add(ci);

                Msg_S2C_JoinRoomResult resultMsg = new Msg_S2C_JoinRoomResult { result = 0 };
                conn.Send(resultMsg);
            }

            if (m_joinRoomMsgs.Count == NetworkServer.connections.Count)
            {
                OnStartRunning();
                m_joinRoomMsgs.Clear();
            }
        }
        
        private void OnMsgPlayerInput(NetworkConnection conn, Msg_C2S_PlayerInput msg)
        {
            Debug.Log($"[server] tick {CurTick} | {msg}: client tick={msg.tick}");

            List<PlayerInput> val;
            if (m_inputs.TryGetValue(msg.tick, out val))
            {
                // todo: better solution
                if (val.FindIndex((PlayerInput pi) => { return pi.playerId == msg.input.playerId; }) < 0)
                {
                    val.Add(msg.input);
                }
            }
            else
            {
                m_inputs.Add(msg.tick, new List<PlayerInput> { msg.input });
            }
        }

        private void OnMsgExitRoom(NetworkConnection conn, Msg_C2S_ExitRoom msg)
        {
            Debug.Log($"[server] tick {CurTick} | {msg}: player id={msg.playerId}");

            int index = m_allClients.FindIndex((ClientInfo ci) => { return ci.playerId == msg.playerId; });
            if (index < 0)
                return;

            m_allClients.RemoveAt(index);

            if (m_allClients.Count == 0)
            {
                OnShutdown();
                NetworkServer.DisconnectAll();

                return;
            }

            Msg_S2C_PlayerExit exitMsg = new Msg_S2C_PlayerExit { playerId = msg.playerId };
            foreach (ClientInfo ci in m_allClients)
            {
                ci.conn.Send(exitMsg);
            }
        }

        #endregion


        #region Process Events

        private void OnClientDisconn(NetworkConnection conn)
        {
            ClientInfo clientInfo = m_allClients.Find((ClientInfo ci) => { return ci.conn == conn; });
            if (clientInfo is null)
                return;

            Debug.Log($"[server] tick {CurTick} | disconnection event: player id={clientInfo.playerId}");

            Msg_C2S_ExitRoom exitMsg = new Msg_C2S_ExitRoom { playerId = clientInfo.playerId };
            OnMsgExitRoom(conn, exitMsg);
        }

        #endregion


        #region Lifecycle

        private void OnSetup()
        {
            if (HasSetup)
                return;

            Debug.Log($"[server] setup");

            HasSetup = true;
            IsRunning = false;
            m_inputs.Clear();
            m_allClients.Clear();
            m_nextPlayerId = 0;
            m_joinRoomMsgs = new List<Msg_C2S_JoinRoom>();
            m_inputs.Clear();

            NetworkServer.OnDisconnectedEvent += OnClientDisconn;
            NetworkServer.RegisterHandler<Msg_C2S_JoinRoom>(OnMsgJoinRoom);
            NetworkServer.RegisterHandler<Msg_C2S_PlayerInput>(OnMsgPlayerInput);
            NetworkServer.RegisterHandler<Msg_C2S_ExitRoom>(OnMsgExitRoom);
        }

        private void OnStartRunning()
        {
            if (IsRunning)
                return;

            Debug.Log($"[server] start running");

            IsRunning = true;
            CurTick = 0;
            m_lastTime = Time.time;

            SendGameStartMsg();
        }

        private void OnUpdate()
        {
            if (!IsRunning)
                return;

            float delta = Time.time - m_lastTime;
            while (delta >= FrameDelta)
            {
                delta -= FrameDelta;
                m_lastTime = Time.time;

                Step();
            }
        }

        private void Step()
        {
            FrameInput finput;
            if (CheckInput(CurTick, out finput))
            {
                BroadcastInput(finput);
                m_inputs.Remove(CurTick);

                ++CurTick;
            }
        }

        private void OnShutdown()
        {
            if (!HasSetup)
                return;

            Debug.Log($"[server] shutdown");

            HasSetup = false;
            IsRunning = false;
            m_inputs.Clear();
            m_allClients.Clear();

            NetworkServer.OnDisconnectedEvent -= OnClientDisconn;
            NetworkServer.UnregisterHandler<Msg_C2S_JoinRoom>();
            NetworkServer.UnregisterHandler<Msg_C2S_PlayerInput>();
            NetworkServer.UnregisterHandler<Msg_C2S_ExitRoom>();
        }

        #endregion

        private bool CheckInput(int tick, out FrameInput finput)
        {
            finput = new FrameInput();

            List<PlayerInput> val;
            bool hasInput = m_inputs.TryGetValue(tick, out val);
            if (hasInput && val.Count == PlayerCount)
            {
                finput.tick = tick;
                finput.inputs = val.ToArray();

                return true;
            }

            return false;
        }

        private void BroadcastInput(FrameInput finput)
        {
            Msg_S2C_FrameInput msg = new Msg_S2C_FrameInput { input = finput };
            NetworkServer.SendToAll(msg);
        }

        private void SendGameStartMsg()
        {
            Msg_S2C_GameStart msg = new Msg_S2C_GameStart();
            msg.players = new PlayerInfo[PlayerCount];

            for (int i = 0; i < PlayerCount; ++i)
            {
                msg.players[i] = SpawnPlayer(i);
            }

            NetworkServer.SendToAll(msg);
        }

        private PlayerInfo SpawnPlayer(int i)
        {
            PlayerInfo info = new PlayerInfo();

            info.connHash = m_joinRoomMsgs[i].connHash;
            info.playerId = m_allClients[i].playerId;

            Vector2 xz = Random.insideUnitSphere * 5.0f;
            info.originPos = new Vector3(xz.x, 0.0f, xz.y);

            float yaw = Random.Range(0.0f, 360.0f);
            info.originRotat = new Vector3(0.0f, yaw, 0.0f);

            return info;
        }


        #region Unity Functions

        private void Start()
        {
            OnSetup();
        }

        private void Update()
        {
            OnUpdate();
        }

        private void OnDestroy()
        {
            OnShutdown();
        }

        #endregion

    }

}