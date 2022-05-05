using UnityEngine;

public class PingNetMonitor : MonoBehaviour
{

    public string IP = "127.0.0.1";
    Ping ping;
    float delayTime;

    void Start()
    {
        SendPing();
    }

    void OnGUI()
    {        
        GUI.color = delayTime < 10 ? Color.green : Color.red;
        GUI.Label(new Rect(10, 10, 100, 20), "net delay: " + delayTime.ToString() + "ms");

        if (null != ping && ping.isDone)
        {
            delayTime = ping.time;
            ping.DestroyPing();
            ping = null;
            Invoke("SendPing", 0.5F);
        }
    }

    void SendPing()
    {
        ping = new Ping(IP);
    }
}