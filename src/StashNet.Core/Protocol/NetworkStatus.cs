using System.Collections.Generic;

namespace StashNet.Core.Protocol
{
    /// <summary>The status lines a terminal shows under a listing: why the network is empty, and what was skipped.</summary>
    public static class NetworkStatus
    {
        public static List<TerminalMessage> Messages(int bays, int otherCoreBays, int drives, int skippedBays, int skippedDrives)
        {
            var messages = new List<TerminalMessage>();
            // M8: bays in reach that belong to a nearer Core are not "no bays": say where they went.
            if (bays == 0 && otherCoreBays > 0) messages.Add(new TerminalMessage("stashnetBaysOtherCore", otherCoreBays));
            else if (bays == 0) messages.Add(new TerminalMessage("stashnetNoBays", -1));
            else if (drives == 0) messages.Add(new TerminalMessage("stashnetNoDrives", -1));
            if (skippedBays > 0) messages.Add(new TerminalMessage("stashnetSkippedBays", skippedBays));
            if (skippedDrives > 0) messages.Add(new TerminalMessage("stashnetSkippedDrives", skippedDrives));
            return messages;
        }
    }
}
