using UnityEngine;
using Mirror;

public class DebugDisplay : MonoBehaviour
{
    public LatencySimulation latencySim;

    private void OnGUI()
    {
        GUILayout.Label("latency spike mul: " + latencySim.latencySpikeMultiplier);
        GUILayout.Label("latency spike speed mul: " + latencySim.latencySpikeSpeedMultiplier);
        GUILayout.Label("reliable latency: " + latencySim.reliableLatency);
        GUILayout.Label("unreliable loss: " + latencySim.unreliableLoss);
        GUILayout.Label("unreliable latency: " + latencySim.unreliableLatency);
        GUILayout.Label("unreliable scramble: " + latencySim.unreliableScramble);
    }
}
