using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Mirror;

namespace NetSyncLab.Lockstep
{

    public class Client : MonoBehaviour
    {
        [Header("UI")]
        public GameObject joinRoomUI;
        public GameObject exitRoomUI;
        public GameObject tipUI;
        public Text tipText;

        [Header("Player")]
        public GameObject playerPrefab;
        public float moveSpeed = 1.0f;

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

        public int PlayerId { get => m_playerId; set => m_playerId = value; }
        private int m_playerId = -1;

        public GameObject Player { get; private set; }

        private float m_lastTime;
        private Dictionary<int, FrameInput> m_inputs = new Dictionary<int, FrameInput>();

        private Dictionary<int, GameObject> m_allPlayers = new Dictionary<int, GameObject>();


        #region Process Messages

        private void OnMsgJoinRoomResult(Msg_S2C_JoinRoomResult msg)
        {
            Debug.Log($"[client] tick {CurTick} | {msg}: result={msg.result}");

            switch (msg.result)
            {
                case 0:
                    OnJoinRoomSucc();
                    break;
                default:
                    OnJoinRoomFail(msg.result);
                    break;
            }
        }

        private void OnMsgGameStart(Msg_S2C_GameStart msg)
        {
            joinRoomUI?.SetActive(false);
            exitRoomUI?.SetActive(true);

            SpawnPlayers(msg.players);

            foreach (PlayerInfo info in msg.players)
            {
                if (info.connHash == GetHashCode())
                {
                    PlayerId = info.playerId;
                    Player = m_allPlayers[PlayerId];
                }
            }

            OnStartRunning();

            Debug.Log($"[client] tick {CurTick} | {msg}: local player id={PlayerId}, player count={msg.players.Length}");
        }

        private void OnMsgFrameInput(Msg_S2C_FrameInput msg)
        {
            Debug.Log($"[client] tick {CurTick} | {msg}: server tick={msg.input.tick}");

            m_inputs.Add(msg.input.tick, msg.input);
        }

        private void OnMsgPlayerExit(Msg_S2C_PlayerExit msg)
        {
            Debug.Log($"[client] tick {CurTick} | {msg}: player id={msg.playerId}");

            GameObject player;
            if (m_allPlayers.TryGetValue(msg.playerId, out player))
            {
                m_allPlayers.Remove(msg.playerId);
                Destroy(player);
            }
        }

        #endregion


        #region Process Events

        private void OnDisconn()
        {
            Debug.Log($"[client] tick {CurTick} | disconnection event");

            OnShutdown();
        }

        #endregion


        #region Lifecycle

        private void OnSetup()
        {
            if (HasSetup)
                return;

            Debug.Log($"[client] setup");

            joinRoomUI?.GetComponentInChildren<Button>().onClick.AddListener(JoinRoom);
            joinRoomUI?.SetActive(true);
            exitRoomUI?.GetComponentInChildren<Button>().onClick.AddListener(ExitRoom);
            exitRoomUI?.SetActive(false);

            HasSetup = true;
            IsRunning = false;
            m_inputs.Clear();
            m_allPlayers.Clear();

            NetworkClient.OnDisconnectedEvent += OnDisconn;
            NetworkClient.RegisterHandler<Msg_S2C_JoinRoomResult>(OnMsgJoinRoomResult);
            NetworkClient.RegisterHandler<Msg_S2C_GameStart>(OnMsgGameStart);
            NetworkClient.RegisterHandler<Msg_S2C_FrameInput>(OnMsgFrameInput);
            NetworkClient.RegisterHandler<Msg_S2C_PlayerExit>(OnMsgPlayerExit);
        }

        private void OnStartRunning()
        {
            Debug.Log($"[client] start running");

            tipUI?.SetActive(false);

            IsRunning = true;
            m_lastTime = Time.time;
            CurTick = 0;
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
            SendInput();

            FrameInput finput;
            if (TryGetFrameInput(CurTick, out finput))
            {
                ProcessFrameInput(finput);
                m_inputs.Remove(CurTick);

                ++CurTick;
            }
        }

        private void OnShutdown()
        {
            if (!HasSetup)
                return;

            Debug.Log($"[client] shutdown");

            HasSetup = false;
            IsRunning = false;
            m_inputs.Clear();

            foreach (GameObject player in m_allPlayers.Values)
            {
                Destroy(player);
            }
            m_allPlayers.Clear();

            NetworkClient.OnDisconnectedEvent -= OnDisconn;
            NetworkClient.UnregisterHandler<Msg_S2C_JoinRoomResult>();
            NetworkClient.UnregisterHandler<Msg_S2C_GameStart>();
            NetworkClient.UnregisterHandler<Msg_S2C_FrameInput>();
            NetworkClient.UnregisterHandler<Msg_S2C_PlayerExit>();
        }

        #endregion

        public void JoinRoom()
        {
            Msg_C2S_JoinRoom msg = new Msg_C2S_JoinRoom { connHash = GetHashCode() };
            NetworkClient.Send(msg);
        }

        public void ExitRoom()
        {
            Msg_C2S_ExitRoom msg = new Msg_C2S_ExitRoom { playerId = PlayerId };
            NetworkClient.Send(msg);

            OnShutdown();

            exitRoomUI?.SetActive(false);
        }

        private void OnJoinRoomSucc()
        {
            joinRoomUI?.SetActive(false);
            if (tipText)
                tipText.text = "Loading...";
            tipUI?.SetActive(true);
        }

        private void OnJoinRoomFail(int err)
        {
            if (tipText)
                tipText.text = $"Failed to join a room. Error code: {err}";
            tipUI?.SetActive(true);
        }

        private void SpawnPlayers(PlayerInfo[] infos)
        {
            foreach (PlayerInfo info in infos)
            {
                GameObject go = SpawnPlayer(info);
                m_allPlayers.Add(info.playerId, go);
            }
        }

        private GameObject SpawnPlayer(PlayerInfo info)
        {
            GameObject go = Instantiate(playerPrefab);

            go.name = $"Unit[playerId={info.playerId}]";
            go.transform.position = info.originPos;
            go.transform.rotation = Quaternion.Euler(info.originRotat);

            return go;
        }

        private void SendInput()
        {
            PlayerInput pinput = CheckInput();
            Msg_C2S_PlayerInput msg = new Msg_C2S_PlayerInput
            {
                tick = CurTick,
                input = pinput
            };
            NetworkClient.Send(msg);
        }

        private PlayerInput CheckInput()
        {
            PlayerInput input = new PlayerInput { playerId = PlayerId };

            float hori = Input.GetAxis("Horizontal");
            float verti = Input.GetAxis("Vertical");
            Vector3 vaxis = new Vector3(hori, 0.0f, verti);

            input.moveInput = vaxis;

            return input;
        }

        private bool TryGetFrameInput(int tick, out FrameInput finput)
        {
            if (m_inputs.TryGetValue(tick, out finput))
            {
                return true;
            }

            finput = new FrameInput();
            return false;
        }

        private void ProcessFrameInput(FrameInput input)
        {
            foreach (PlayerInput pinput in input.inputs)
            {
                int pId = pinput.playerId;
                GameObject player;
                if (m_allPlayers.TryGetValue(pId, out player))
                {
                    Vector3 vaxis = pinput.moveInput;
                    // todo
                    player.transform.Translate(vaxis * moveSpeed, Space.World);
                }
            }
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