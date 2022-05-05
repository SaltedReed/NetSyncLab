using UnityEngine;
using UnityEngine.UI;
using Mirror;

public class CmdHandler : MonoBehaviour
{
    public InputField inputf;
    public LatencySimulation latencySim;

    private void Start()
    {
        inputf.onEndEdit.AddListener(OnCmdSubmit);
    }

    public void OnCmdSubmit(string cmd)
    {
        string[] tokens = cmd.ToLower().Split(' ');
        string cmdname = tokens[0];
        Debug.Log("cmd " + cmdname);

        if (cmdname == "latspimul")
        {
            float val = float.Parse(tokens[1]);
            latencySim.latencySpikeMultiplier = val;
        }
        else if (cmdname=="latspispemul")
        {
            float val = float.Parse(tokens[1]);
            latencySim.latencySpikeSpeedMultiplier = val;
        }
        else if (cmdname == "rellat")
        {
            float val = float.Parse(tokens[1]);
            latencySim.reliableLatency = val;
        }
        else if (cmdname == "unrlos")
        {
            float val = float.Parse(tokens[1]);
            latencySim.unreliableLoss = val;
        }
        else if (cmdname == "unrlat")
        {
            float val = float.Parse(tokens[1]);
            latencySim.unreliableLatency = val;
        }
        else if (cmdname == "unrscr")
        {
            float val = float.Parse(tokens[1]);
            latencySim.unreliableScramble = val;
        }
    }

}
