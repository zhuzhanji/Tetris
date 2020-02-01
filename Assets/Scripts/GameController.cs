﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;
using Vector3 = UnityEngine.Vector3;
using Quaternion = UnityEngine.Quaternion;

public class GameController : MonoBehaviour {
    private float previousTime;
    private float previousToLeft;
    private float previousToRight;
    public float fallTime = 0.8f;
    public float N = 10;
    public Vector3 startPos = new Vector3();
    public static int height = 20;
    public static int width = 10;
    private static int score = 0;
    private static int linesDeleted = 0;
    private static int numGems = 0;
    private static int[] scores = {0,40,100,300,1200};
    private static readonly string textFile = Path.GetFullPath("Assets/Stages/Easy.txt");
    private static HashSet<int> deck = new HashSet<int>();

    private static Block[,] grid = new Block[height, width];
    private static int[,] stage = new int[height, width];

    public TetrisBlock[] Blocks;
    private Vector3[] Pivots = new[] { new Vector3(-0.33f, 0f, 0f), new Vector3(-0.27f, -0.15f, 0f), new Vector3(-0.27f, 0.1f, 0f), new Vector3(-0.12f, -0.1f, 0f), new Vector3(-0.22f, -0.1f, 0f), new Vector3(-0.02f, -0.1f, 0f), new Vector3(-0.2f, 0.1f, 0f) };

    public GhostBlock[] Ghosts;
    private int nextBlock;
    public TetrisBlock nextBlockObject;
    public TetrisBlock currBlock;
    public TetrisBlock deadBlock;
    public GameObject nextBlockBackground;
    public GemBlock gemBlock;
    private GhostBlock ghostBlock;
    private bool hardDropped, gameOver, gameClear, isDestroying;
    private ModeController controller;

    public Text timeValue, levelValue, linesValue, highscoreValue, scoreValue, gameModeValue;

    void Awake() {
        controller = GameObject.FindWithTag("ModeController").GetComponent<ModeController>();
        gameModeValue.text = (controller.GetMode() == ModeController.Mode.stage ? "S T A G E" : "I N F I N I T E") + "  M O D E";
    }

    void Start() {
        NextBlock();
        if (controller.GetMode() == ModeController.Mode.stage) SetStage();
        NewBlock();
    }

    void NextBlock() {
        if (deck.Count == Blocks.Length) deck.Clear();
        do nextBlock = Random.Range(0, Blocks.Length);
        while (deck.Contains(nextBlock));
        deck.Add(nextBlock);

        if (nextBlockObject != null) nextBlockObject.Destroy();
        nextBlockObject = Instantiate(Blocks[nextBlock]);
        nextBlockObject.transform.parent = nextBlockBackground.transform;
        nextBlockObject.transform.localPosition = Pivots[nextBlock];
    }

    void SetStage() {
        if (File.Exists(textFile)) {
            string[] lines = File.ReadAllLines(textFile);
            for (int y = 0; y < height; y++) {
                string[] pixels  = lines[y].Split(',');
                for (int x = 0; x < width; x++) {
                    int blockType = Int16.Parse(pixels[x]);
                    switch (blockType) {
                        case 1:
                            grid[y, x] = Instantiate(deadBlock, new Vector3(x, y, 0), Quaternion.identity);
                            break;
                        case 2:
                            numGems++;
                            grid[y, x] = Instantiate(gemBlock, new Vector3(x, y, 0), Quaternion.identity);
                            break;
                    }
                    stage[y, x] = blockType;
                }
            }
        } else {
            print(String.Format("File {0} does not exist!", textFile));
        }
    }

    void Update() {
        if (!gameOver && !gameClear) {
            if (Input.GetKey(KeyCode.LeftArrow) && Time.time - previousToLeft > 0.08f) {
                HorizontalMove(Vector3.left);
                previousToLeft = Time.time;
            } else if (Input.GetKey(KeyCode.RightArrow) && Time.time - previousToRight > 0.08f) {
                HorizontalMove(Vector3.right);
                previousToRight = Time.time;
            } else if (Input.GetKeyDown(KeyCode.UpArrow)) {
                Rotate();
            } else if (Input.GetKeyDown(KeyCode.Space)) {
                while (ValidMove(currBlock.transform) && !hardDropped) VerticalMove(Vector3.down);
            } else if (Input.GetKeyUp(KeyCode.Space)) {
                hardDropped = false;
            }

            if (Time.time - previousTime > (Input.GetKey(KeyCode.DownArrow) ? fallTime / 10 : fallTime)) {
                VerticalMove(Vector3.down);
                previousTime = Time.time;
            }
            if (!ghostBlock.IsDestroyed()) ghostBlock.transform.position = GhostPosition(currBlock.transform.position);

            int nextLevel = Mathf.RoundToInt((linesDeleted / N) + 1);

            if (Int16.Parse(levelValue.text) < nextLevel) fallTime /= 1f + (Mathf.RoundToInt(linesDeleted / N) * 0.1f);

            timeValue.text = Time.time.ToString();
            levelValue.text = nextLevel.ToString();
            linesValue.text = linesDeleted.ToString();
            highscoreValue.text = score.ToString();
            scoreValue.text = score.ToString();
        }
    }

    void Rotate() {
        Transform currTransform = currBlock.transform;
        currTransform.RotateAround(currTransform.TransformPoint(currBlock.rotationPoint), Vector3.forward, 90);
        ghostBlock.transform.RotateAround(ghostBlock.transform.TransformPoint(currBlock.rotationPoint), Vector3.forward, 90);

        if (!ValidMove(currBlock.transform)) {
            currTransform.RotateAround(currTransform.TransformPoint(currBlock.rotationPoint), Vector3.forward, -90);
            ghostBlock.transform.RotateAround(ghostBlock.transform.TransformPoint(currBlock.rotationPoint), Vector3.forward, -90);

        }
    }

    void HorizontalMove(Vector3 nextMove) {
        currBlock.transform.position += nextMove;
        if (!ValidMove(currBlock.transform)) {
            currBlock.transform.position -= nextMove;
        }
    }

    void VerticalMove(Vector3 nextMove) {
        print(currBlock.transform.position);
        currBlock.transform.position += nextMove;
        if (!ValidMove(currBlock.transform)) {
            currBlock.transform.position -= nextMove;
            EndTurn();
        }
    }

    private void EndTurn() {
        try {
            AddToGrid();
            CheckForLines();
            hardDropped = true;
        } catch (GameOverException e) {
            GameOver();
        } catch (GameClearException e) {
            GameClear();
        }
    }

    void AddToGrid() {
        foreach (Transform children in currBlock.transform) {
            int roundedY = Mathf.RoundToInt(children.transform.position.y);
            int roundedX = Mathf.RoundToInt(children.transform.position.x);
            Color currColor = children.GetComponent<SpriteRenderer>().color;
            TetrisBlock curr = Instantiate(deadBlock, new Vector3(roundedX, roundedY, 0), Quaternion.identity);
            curr.sprite.GetComponent<SpriteRenderer>().color = currColor;
            grid[roundedY, roundedX] = curr;
            currBlock.Destroy();
            ghostBlock.Destroy();

            if (roundedX == 4 && roundedY == 18) gameOver = true;
        }
        if (gameOver) throw new GameOverException();
    }

    void CheckForLines() {
        int numLines = 0;
        for (int y = height - 1; y >= 0; y--) {
            if (HasLine(y)) {
                numLines++;
                StartCoroutine(DeleteLine(y));
                StartCoroutine(WaitForRowDown(y));
            }
        }
        score += scores[numLines] * Mathf.RoundToInt((linesDeleted / N) + 1);
        linesDeleted += numLines;
        StartCoroutine(WaitForNewBlock());
    }

    private IEnumerator WaitForNewBlock() {
        while (isDestroying) {
            yield return new WaitForSeconds(0.03f);
        }
        NewBlock();
    }

    private IEnumerator WaitForRowDown(int y) {
        while (isDestroying) {
            yield return new WaitForSeconds(0.03f);
        }
        RowDown(y);
    }

    bool HasLine(int y) {
        for (int x = 0; x < width; x++) {
            if (grid[y, x] == null) return false;
        }
        return true;
    }                           

    private IEnumerator DeleteLine(int y) {
        isDestroying = true;
        int[] destroyedBlocks = new int[1];
        destroyedBlocks[0] = 0;
        for (int x = 0; x < width; x++) {
            if (grid[y, x] != null) {
                StartCoroutine(DeleteLineEffect(grid[y, x], destroyedBlocks));
                yield return new WaitForSeconds(0.05f);
            }
        }

        while (destroyedBlocks[0] < 10) {
            yield return new WaitForSeconds(0.05f);
        }
        for (int x = 0; x < width; x++) {
            if (grid[y, x] != null) {
                grid[y, x].Destroy();
                grid[y, x] = null;
                if (stage[y, x] == 2) if (--numGems == 0) gameClear = true;
            }
        }

        isDestroying = false;
        destroyedBlocks[0] = 0;
    }

    private IEnumerator DeleteLineEffect(Block dead, int[] destroyedBlocks) {
        Color tmp = dead.sprite.GetComponent<SpriteRenderer>().color;
        float _progress = 1f;

        while (_progress > 0.0f) {
            dead.sprite.GetComponent<SpriteRenderer>().color = new Color(tmp.r, tmp.g, tmp.b, tmp.a * _progress);
            _progress -= 0.1f;
            yield return new WaitForSeconds(0.03f);
        }

        if (_progress < 0.0f && dead != null) {
            destroyedBlocks[0]++;
        }
    }

    void RowDown(int deletedLine) {
        for (int y = deletedLine; y < height; y++) {
            for (int x = 0; x < width; x++) {
                if (grid[y, x] != null) {
                    grid[y - 1, x] = grid[y, x];
                    grid[y, x] = null;
                    grid[y - 1, x].transform.position -= Vector3.up;
				}
            }
        }
        if (gameClear) throw new GameClearException();
    }

    bool ValidMove(Transform transform) {
        foreach (Transform children in transform) {
            int roundedY = Mathf.RoundToInt(children.transform.position.y);
            int roundedX = Mathf.RoundToInt(children.transform.position.x);
            if (roundedX < 0 || roundedX >= width || roundedY < 0 || roundedY >= height) {
                return false;
            }
            if (grid[roundedY, roundedX] != null) {
                return false;
            }
        }
        return true;
    }

    public Vector3 GhostPosition(Vector3 vec) {
        int x = Mathf.RoundToInt(vec.x), y = Math.Max(Mathf.RoundToInt(vec.y), 0), z = Mathf.RoundToInt(vec.z);
        ghostBlock.transform.position = new Vector3(x, y, z);
        while (ValidMove(ghostBlock.transform)) ghostBlock.transform.position += Vector3.down;
        
        return ghostBlock.transform.position + Vector3.up;
    }

    private void NewBlock() {
        currBlock = Instantiate(Blocks[nextBlock], startPos, Quaternion.identity);
        NewGhost();
        NextBlock();
    }

    private void NewGhost() {
        if (ghostBlock != null) {
            ghostBlock.Destroy();
            ghostBlock = Instantiate(Ghosts[nextBlock], currBlock.transform.position, Quaternion.identity);
        } else {
            ghostBlock = Instantiate(Ghosts[nextBlock], currBlock.transform.position, Quaternion.identity);
    }
}

    private void GameOver() {
        print("Game Over");
        if (ghostBlock != null) ghostBlock.Destroy();
    }

    private void GameClear() {
        print("Game Clear");
        if (ghostBlock != null) ghostBlock.Destroy();
        // set panel active
    }
}

[Serializable]
public class GameOverException : Exception
{
    public GameOverException() { }

    public GameOverException(string message)
        : base(message) { }
}

[Serializable]
public class GameClearException : Exception
{
    public GameClearException() { }

    public GameClearException(string message)
        : base(message) { }
}
