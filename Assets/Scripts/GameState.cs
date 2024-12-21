using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class GameState
{
    public int score; // The current score of the game
    public List<TileStateInfo> tiles; // Information about all tiles on board

    [System.Serializable]
    public class TileStateInfo {
        public int tileId;
        public Vector2Int savedposition; // Tile's position after move
        public int stateIndex; // Index of the tile state (e.g. "2", "4", etc.)
    }
}
