using StashNet.Core.Protocol;

namespace StashNet.Skins
{
    /// <summary>
    /// PLAYER'S GAME: the live Storage Terminal screens. Fed by ScreenLink with the server's answers.
    /// Filled by the next task (drawing the numbers on each terminal's CRT).
    /// </summary>
    internal static class TerminalScreens
    {
        /// <summary>The terminal at <paramref name="terminal"/> belongs to the Core at <paramref name="core"/> (if hasCore).</summary>
        internal static void OnAssign(Vector3i terminal, bool hasCore, Vector3i core)
        {
            // Filled by the next task.
        }

        /// <summary>The network at <paramref name="core"/> now looks like <paramref name="view"/>.</summary>
        internal static void OnState(Vector3i core, ScreenView view)
        {
            // Filled by the next task.
        }

        /// <summary>Leaving the game: forget every screen.</summary>
        internal static void Clear()
        {
            // Filled by the next task.
        }
    }
}
