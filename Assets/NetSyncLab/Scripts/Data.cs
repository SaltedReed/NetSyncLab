using UnityEngine;

namespace NetSyncLab.Lockstep
{

    public struct PlayerInfo
    {
        public int connHash;
        public int playerId;
        public Vector3 originPos;
        public Vector3 originRotat;
    }


    public struct PlayerInput
    {
        public int playerId;
        public Vector3 moveInput;
    }


    public struct FrameInput
    {
        public int tick;
        public PlayerInput[] inputs;
    }


}