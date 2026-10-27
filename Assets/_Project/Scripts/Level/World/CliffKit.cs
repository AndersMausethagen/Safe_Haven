using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The rock-face pieces a <see cref="CliffFeature"/> stands along its face (Create › Safe Haven › World › Cliff Kit).
/// Scanned rock faces are often open at the back and lean, so each piece knows which way its finished side faces and how
/// far it leans back; the cliff stands it up to face outwards. Measure Pieces (in the Inspector) works both out from the
/// mesh and records the size, and you can correct them by hand.
/// </summary>
[CreateAssetMenu(menuName = "Safe Haven/World/Cliff Kit", fileName = "Cliff Kit")]
public sealed class CliffKit : ScriptableObject
{
    [Serializable]
    public sealed class Piece
    {
        public GameObject prefab;
        [Min(0f)] public float weight = 1f;
        [Tooltip("Turn (degrees about up) from the prefab's forward (+Z) to the way its finished side faces.")]
        public float faceYaw;
        [Tooltip("How far the finished side leans back, facing the sky (degrees); the cliff stands the piece up.")]
        public float faceTilt;
        [Tooltip("Size of the piece stood up at scale 1: x across the face, y height, z depth (metres; measured).")]
        public Vector3 size = new Vector3(3f, 3f, 1.5f);
    }

    [SerializeField] private List<Piece> pieces = new List<Piece>();

    public List<Piece> Pieces => pieces;

    /// <summary>A random piece by weight (null when the kit is empty).</summary>
    public Piece Pick(System.Random random)
    {
        float total = 0f;
        foreach (Piece piece in pieces)
        {
            total += piece.prefab != null ? piece.weight : 0f;
        }

        float roll = (float)random.NextDouble() * total;
        foreach (Piece piece in pieces)
        {
            if (piece.prefab == null)
            {
                continue;
            }

            roll -= piece.weight;
            if (roll <= 0f)
            {
                return piece;
            }
        }

        return null;
    }
}
