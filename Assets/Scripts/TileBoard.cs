using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TileBoard : MonoBehaviour {

    // Declare a stack to store the states
    // Before moving
    private Stack<GameState> previousStates = new Stack<GameState>();
    // After moving
    private Stack<GameState> nextStates = new Stack<GameState>();


    [SerializeField] private Tile tilePrefab;
    [SerializeField] private TileState[] tileStates;


    private TileGrid grid;
    private List<Tile> tiles;
    private bool waiting;

    // Save every moved tile info, before sending to stack
    private List<SaveMovedTileInfo> movedTiles;

    private void Awake() {
        grid = GetComponentInChildren<TileGrid>();
        tiles = new List<Tile>(16);
        movedTiles = new List<SaveMovedTileInfo>(16);
    }

    public void ClearBoardHistory() {
        // Clear stacks
        previousStates.Clear();
        nextStates.Clear();
    }

    public void ClearBoard() {

        foreach (var cell in grid.cells) {
            cell.tile = null;
        }

        foreach (var tile in tiles) {
            Destroy(tile.gameObject);
        }

        // Clear lists
        tiles.Clear();
        movedTiles.Clear();
    }

    public void CreateTile() {
        Tile tile = Instantiate(tilePrefab, grid.transform);
        tile.SetState(tileStates[0]);
        tile.Spawn(grid.GetRandomEmptyCell());
        tiles.Add(tile);
    }

    private void Update() {
        if (waiting) return;

        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) {
            Move(Vector2Int.up, 0, 1, 1, 1);
        } else if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) {
            Move(Vector2Int.left, 1, 1, 0, 1);
        } else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) {
            Move(Vector2Int.down, 0, 1, grid.Height - 2, -1);
        } else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) {
            Move(Vector2Int.right, grid.Width - 2, -1, 0, 1);
        } else if (Input.GetKeyDown(KeyCode.Z)) { // Undo key
            Undo();
        }

    }
    // Safety check, if same tile info, will be send for saving. Do not allow save same data twice
    private bool IsTileAlreadySaved(int tileInstanceID, Vector2Int coordinates, int stateIndex) {
        foreach (var tileData in movedTiles) {
            if (tileData.coordinates == coordinates && tileData.stateIndex == stateIndex && tileData.tileId == tileInstanceID) {
                return true;
            }
        }
        return false;
    }

    public void UndoAfterButtonPressed() {
        // If player will press undo, BEFORE move animation was over. Not allow this.
        if (waiting) {
            Debug.Log("Don't rush");
            return;
        }
        Undo();
    }

    private void SaveState() {

        // Save current tileboard state and send to stack

        GameState beforeMoveState = new GameState();
        beforeMoveState.score = GameManager.Instance.score;
        beforeMoveState.tiles = new List<GameState.TileStateInfo>();

        foreach (var tile in tiles) {
            beforeMoveState.tiles.Add(new GameState.TileStateInfo {
                savedposition = tile.cell.coordinates,
                stateIndex = IndexOf(tile.state), // Conver tile state to an index
                tileId = tile.GetInstanceID()
            });
        }
        previousStates.Push(beforeMoveState);
    }

    private void SaveAfterMoveState() {

        // Save current tileboard state and send to stack(after tiles moved)

        GameState afterMoveState = new GameState();
        afterMoveState.score = GameManager.Instance.score;
        afterMoveState.tiles = new List<GameState.TileStateInfo>();

        
        foreach (var tileData in movedTiles) {
            afterMoveState.tiles.Add(new GameState.TileStateInfo {
                savedposition = tileData.coordinates,
                stateIndex = tileData.stateIndex, // Conver tile state to an index
                tileId = tileData.tileId
            });
            //Debug.Log("MOVED");
        }

        // In case some tiles dont move an don't merge, save them too.
        foreach (var tile in tiles) {
            if (!tile.hasChanged) {
                afterMoveState.tiles.Add(new GameState.TileStateInfo {
                    savedposition = tile.cell.coordinates,
                    stateIndex = IndexOf(tile.state), // Conver tile state to an index
                    tileId = tile.GetInstanceID()
                });
            }
            //Debug.Log("STAYED");
        }

        movedTiles.Clear();

        nextStates.Push(afterMoveState);
    }

    private void Move(Vector2Int direction, int startX, int incrementX, int startY, int incrementY) {

        SaveState(); // Save the current state before moving tiles

        bool changed = false;

        for (int x = startX; x >= 0 && x < grid.Width; x += incrementX) {
            for (int y = startY; y >= 0 && y < grid.Height; y += incrementY) {
                TileCell cell = grid.GetCell(x, y);

                if (cell.Occupied) {
                    changed |= MoveTile(cell.tile, direction);
                }
            }
        }

        SaveAfterMoveState();
        movedTiles.Clear();

        if (NothingChanged()) {
            previousStates.Pop();
            nextStates.Pop();
        };

        if (changed) {
            StartCoroutine(WaitForChanges());
        }
    }

    // In case no tile is moved. Don't need to save this situations in stack
    private bool NothingChanged() {
        for(int i = 0; i < tiles.Count; i++) {
            if (tiles[i].hasChanged) {
                return false;
            }
        }
        return true;
    }

    private void Undo() {

        waiting = true;

        if (nextStates.Count == 0 || previousStates.Count == 0) {
            waiting = false;
            return; // No state to undo
        }

        GameState previousState = previousStates.Pop();
        GameState nextState = nextStates.Pop();

        // Case where we don't move yet. After game started.
        if (nextState.tiles.Count == 0) {
            waiting = false;
            return;
        }

        // Clear the board
        ClearBoard();

        // Restore the previous score
        GameManager.Instance.ReturnPreviousScore(previousState.score);

        // Restore and move tiles
        foreach (var previousTileState in previousState.tiles) {
            // Find matching tile in nextState by tileId
            var matchingTiles = nextState.tiles
                .FirstOrDefault(t => t.tileId == previousTileState.tileId);

            if (matchingTiles != null) {
                Tile tile = Instantiate(tilePrefab, grid.transform);
                tile.SetState(tileStates[previousTileState.stateIndex]);

                // Restore tile's last position from nextState
                TileCell nexTileCell = grid.GetCell(matchingTiles.savedposition.x, matchingTiles.savedposition.y);

                // Set tile's previous position from previousState
                TileCell previousTileCell = grid.GetCell(previousTileState.savedposition.x, previousTileState.savedposition.y);

                tile.Spawn(nexTileCell);

                tile.MoveTo(previousTileCell);

                tiles.Add(tile);

            }

        }

        waiting = false;

    }

    private bool MoveTile(Tile tile, Vector2Int direction) {

        TileCell newCell = null;
        TileCell adjacent = grid.GetAdjacentCell(tile.cell, direction);

        while (adjacent != null) {
            if (adjacent.Occupied) {
                if (CanMerge(tile, adjacent.tile)) {
                    MergeTiles(tile, adjacent.tile);
                    tile.hasChanged = true;
                    return true;
                }

                break;
            }

            newCell = adjacent;
            adjacent = grid.GetAdjacentCell(adjacent, direction);

        }

        if (newCell != null) {
            tile.MoveTo(newCell);
            tile.hasChanged = true;
            if (!IsTileAlreadySaved(tile.GetInstanceID(), tile.cell.coordinates, IndexOf(tile.state))) {
                movedTiles.Add(new SaveMovedTileInfo(tile.GetInstanceID(), tile.cell.coordinates, IndexOf(tile.state)));
                //Debug.Log($"Saved tile <<zzzzzz>>: Coordinates {tile.cell.coordinates}, StartIndex {IndexOf(tile.state)}");
            } else {
                //Debug.Log($"Tile already exists in tiles2");
            }
            return true;
        }

        return false;
    }

    private bool CanMerge(Tile a, Tile b) {
        return a.state == b.state && !b.locked;
    }

    private void MergeTiles(Tile a, Tile b) {

        a.hasChanged = true;
        b.hasChanged = true;

        if (!IsTileAlreadySaved(a.GetInstanceID(), b.cell.coordinates, IndexOf(a.state))) {

            movedTiles.Add(new SaveMovedTileInfo(a.GetInstanceID(), b.cell.coordinates, IndexOf(a.state)));
            //Debug.Log($"Saved tile <<a>> to tiles2: Coordinates {b.cell.coordinates}, StartIndex {b.state}");
        } else {
            //Debug.Log($"Tile already exists in tiles2");
        }

        if (!IsTileAlreadySaved(GetInstanceID(), b.cell.coordinates, IndexOf(b.state))) {

            movedTiles.Add(new SaveMovedTileInfo(b.GetInstanceID(), b.cell.coordinates, IndexOf(b.state)));
            //Debug.Log($"Saved tile <<b>> to tiles2: Coordinates {b.cell.coordinates}, StartIndex {b.state}");
        } else {
            //Debug.Log($"Tile already exists in tiles2");
        }

        tiles.Remove(a);
        a.Merge(b.cell);

        int index = Mathf.Clamp(IndexOf(b.state) + 1, 0, tileStates.Length - 1);
        TileState newState = tileStates[index];

        b.SetState(newState);
        //Debug.Log("New merged tile name is: " + b.state);
        GameManager.Instance.IncreaseScore(newState.number);

    }

    private int IndexOf(TileState state) {
        for (int i = 0; i < tileStates.Length; i++) {
            if (state == tileStates[i]) {
                return i;
            }
        }

        return -1;
    }

    private IEnumerator WaitForChanges() {
        waiting = true;

        yield return new WaitForSeconds(0.1f);

        waiting = false;

        foreach (var tile in tiles) {
            tile.locked = false;
            tile.hasChanged = false;
        }

        if (tiles.Count != grid.Size) {
            CreateTile();
        }

        if (CheckForGameOver()) {
            GameManager.Instance.GameOver();
        }
    }

    public bool CheckForGameOver() {
        if (tiles.Count != grid.Size) {
            return false;
        }

        foreach (var tile in tiles) {
            TileCell up = grid.GetAdjacentCell(tile.cell, Vector2Int.up);
            TileCell down = grid.GetAdjacentCell(tile.cell, Vector2Int.down);
            TileCell left = grid.GetAdjacentCell(tile.cell, Vector2Int.left);
            TileCell right = grid.GetAdjacentCell(tile.cell, Vector2Int.right);

            if (up != null && CanMerge(tile, up.tile)) {
                return false;
            }

            if (down != null && CanMerge(tile, down.tile)) {
                return false;
            }

            if (left != null && CanMerge(tile, left.tile)) {
                return false;
            }

            if (right != null && CanMerge(tile, right.tile)) {
                return false;
            }
        }

        return true;
    }

}
