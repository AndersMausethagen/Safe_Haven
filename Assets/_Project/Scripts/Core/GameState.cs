// High-level, mutually exclusive gameplay modes owned by GameStateManager.
// Orthogonal conditions (paused, jumping, attacking, stunned, interacting) are not modes and belong to their own systems.
// Append new modes at the end so values serialized in scenes stay valid.
public enum GameState
{
    Exploration = 0,
    Combat = 1
}
