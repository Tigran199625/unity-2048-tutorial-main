using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TileBoard : MonoBehaviour
{
    // Declare a stack to store the states
    private Stack<GameState> previousStates = new Stack<GameState>();


    [SerializeField] private Tile tilePrefab;
    [SerializeField] private TileState[] tileStates;

    private TileGrid grid;
    private List<Tile> tiles;
    private bool waiting;

    private void Awake()
    {
        grid = GetComponentInChildren<TileGrid>();
        tiles = new List<Tile>(16);
    }

    public void ClearBoardHistory() {
        previousStates.Clear(); // Clear stack
    }

    public void ClearBoard()
    {

        foreach (var cell in grid.cells) {
            cell.tile = null;
        }

        foreach (var tile in tiles) {
            Destroy(tile.gameObject);
        }

        tiles.Clear();
    }

    public void CreateTile()
    {
        Tile tile = Instantiate(tilePrefab, grid.transform);
        tile.SetState(tileStates[0]);
        tile.Spawn(grid.GetRandomEmptyCell());
        tiles.Add(tile);
    }


    private void Update()
    {
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

    // Call "Undo" method with button
    public void UndoAfterButtonPressed() {
        Undo();
    }

    private void SaveState() {

        GameState state = new GameState();
        state.score = GameManager.Instance.score;
        state.tiles = new List<GameState.TileStateInfo>();

        foreach (var tile in tiles) {
            state.tiles.Add(new GameState.TileStateInfo {
                position = tile.cell.coordinates,
                stateIndex = IndexOf(tile.state) // Conver tile state to an index
            });
        }
        previousStates.Push(state);
    }

    private void Move(Vector2Int direction, int startX, int incrementX, int startY, int incrementY)
    {

        SaveState(); // Save the current state before moving tiles

        bool changed = false;

        for (int x = startX; x >= 0 && x < grid.Width; x += incrementX)
        {
            for (int y = startY; y >= 0 && y < grid.Height; y += incrementY)
            {
                TileCell cell = grid.GetCell(x, y);

                if (cell.Occupied) {
                    changed |= MoveTile(cell.tile, direction);
                }
            }
        }

        if (changed) {
            StartCoroutine(WaitForChanges());
        }
    }

    public void Undo() {

        if (previousStates.Count == 0) return; // No state to undo

        GameState state = previousStates.Pop();

        // Clear the board
        ClearBoard();

        // Restore the previous score
        GameManager.Instance.ReturnPreviousScore(state.score);

        // Restore tiles
        foreach(var tileInfo in state.tiles) {
            Tile tile = Instantiate(tilePrefab, grid.transform);
            tile.SetState(tileStates[tileInfo.stateIndex]);
            TileCell cell = grid.GetCell(tileInfo.position.x, tileInfo.position.y);
            tile.Spawn(cell);
            tiles.Add(tile);
        }

    }

    private bool MoveTile(Tile tile, Vector2Int direction)
    {
        TileCell newCell = null;
        TileCell adjacent = grid.GetAdjacentCell(tile.cell, direction);

        while (adjacent != null)
        {
            if (adjacent.Occupied)
            {
                if (CanMerge(tile, adjacent.tile))
                {
                    MergeTiles(tile, adjacent.tile);
                    return true;
                }

                break;
            }

            newCell = adjacent;
            adjacent = grid.GetAdjacentCell(adjacent, direction);
        }

        if (newCell != null)
        {
            tile.MoveTo(newCell);
            return true;
        }

        return false;
    }

    private bool CanMerge(Tile a, Tile b)
    {
        return a.state == b.state && !b.locked;
    }

    private void MergeTiles(Tile a, Tile b)
    {
        tiles.Remove(a);
        a.Merge(b.cell);

        int index = Mathf.Clamp(IndexOf(b.state) + 1, 0, tileStates.Length - 1);
        TileState newState = tileStates[index];

        b.SetState(newState);
        GameManager.Instance.IncreaseScore(newState.number);
    }

    private int IndexOf(TileState state)
    {
        for (int i = 0; i < tileStates.Length; i++)
        {
            if (state == tileStates[i]) {
                return i;
            }
        }

        return -1;
    }

    private IEnumerator WaitForChanges()
    {
        waiting = true;

        yield return new WaitForSeconds(0.1f);

        waiting = false;

        foreach (var tile in tiles) {
            tile.locked = false;
        }

        if (tiles.Count != grid.Size) {
            CreateTile();
        }

        if (CheckForGameOver()) {
            GameManager.Instance.GameOver();
        }
    }

    public bool CheckForGameOver()
    {
        if (tiles.Count != grid.Size) {
            return false;
        }

        foreach (var tile in tiles)
        {
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
