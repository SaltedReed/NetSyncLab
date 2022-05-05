using Mirror;

namespace NetSyncLab.Lockstep
{

    public struct Msg_C2S_JoinRoom : NetworkMessage
    {
        // cannot use connectionId, because all of them are zero, no idea why
        public int connHash;
    }


    public struct Msg_S2C_JoinRoomResult : NetworkMessage
    {
        // 0: success, 1: failure
        public int result;
    }


    public struct Msg_S2C_GameStart : NetworkMessage
    {
        public PlayerInfo[] players;
    }


    public struct Msg_C2S_PlayerInput : NetworkMessage
    {
        public int tick;
        public PlayerInput input;
    }


    public struct Msg_S2C_FrameInput : NetworkMessage
    {
        public FrameInput input;
    }


    public struct Msg_C2S_ExitRoom : NetworkMessage
    {
        public int playerId;
    }


    public struct Msg_S2C_PlayerExit : NetworkMessage
    {
        public int playerId;
    }


}