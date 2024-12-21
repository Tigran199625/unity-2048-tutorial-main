using UnityEngine;

[System.Serializable]
public class SaveMovedTileInfo {
    public int tileId;
    public Vector2Int coordinates; 
    public int stateIndex;

    public SaveMovedTileInfo(int tileId, Vector2Int coordinates, int stateIndex) {
        this.tileId = tileId;
        this.coordinates = coordinates;
        this.stateIndex = stateIndex;
    }
}
