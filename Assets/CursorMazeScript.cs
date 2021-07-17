using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KModkit;
using System.Text.RegularExpressions;
using System;

public class CursorMazeScript : MonoBehaviour {

    public KMAudio audio;
    public KMBombInfo bomb;
    public KMSelectable moduleSelectable;
    public GameObject[] tileColliders;
    public Material[] materials;
    private RaycastHit[] allHit;

    private string[] objNames = new string[64];
    private bool[] isPath;
    private bool focused = false;
    private bool inMaze = false;
    private int startTile = -1;
    private int endTile = -1;

    static int moduleIdCounter = 1;
    int moduleId;
    private bool moduleSolved;

    void Awake()
    {
        moduleId = moduleIdCounter++;
        moduleSolved = false;
        for (int i = 0; i < objNames.Length; i++)
        {
            objNames[i] = "PathCM" + moduleId + "-" + i;
            tileColliders[i].name = objNames[i];
        }
        if (Application.isEditor)
            focused = true;
        moduleSelectable.OnFocus += delegate () { focused = true; };
        moduleSelectable.OnDefocus += delegate () { focused = false; };
    }

    void Start() {
        redo:
        isPath = new bool[64];
        int index = UnityEngine.Random.Range(0, objNames.Length);
        startTile = index;
        isPath[index] = true;
        int pathMax = UnityEngine.Random.Range(25, 41);
        int pathCount = 0;
        while (pathCount != pathMax)
        {
            List<int> possibleTiles = new List<int>();
            if ((index - 8) > -1)
            {
                if (ValidTile(index - 8))
                    possibleTiles.Add(index - 8);
            }
            if ((index + 8) < 64)
            {
                if (ValidTile(index + 8))
                    possibleTiles.Add(index + 8);
            }
            if (Modulo(index - 1, 8) != 7)
            {
                if (ValidTile(index - 1))
                    possibleTiles.Add(index - 1);
            }
            if (Modulo(index + 1, 8) != 0)
            {
                if (ValidTile(index + 1))
                    possibleTiles.Add(index + 1);
            }
            if (possibleTiles.Count == 0)
                goto redo;
            index = possibleTiles[UnityEngine.Random.Range(0, possibleTiles.Count)];
            isPath[index] = true;
            pathCount++;
        }
        endTile = index;
        Debug.LogFormat("[Cursor Maze #{0}] Generated Maze:", moduleId);
        for (int i = 0; i < 8; i++)
        {
            string logLine = "";
            for (int j = 0; j < 8; j++)
            {
                if (!isPath[i * 8 + j])
                {
                    tileColliders[i * 8 + j].SetActive(false);
                    logLine += "X";
                }
                else if (startTile == (i * 8 + j))
                {
                    tileColliders[i * 8 + j].GetComponent<Renderer>().material = materials[0];
                    logLine += "G";
                }
                else if (endTile == (i * 8 + j))
                {
                    tileColliders[i * 8 + j].GetComponent<Renderer>().material = materials[1];
                    logLine += "R";
                }
                else
                    logLine += "O";
            }
            Debug.LogFormat("[Cursor Maze #{0}] {1}", moduleId, logLine);
        }
        Debug.LogFormat("[Cursor Maze #{0}] (O = Path | X = Wall | G = Green Square | R = Red Square)", moduleId);
    }

    void Update()
    {
        if (focused && !moduleSolved)
        {
            allHit = Physics.RaycastAll(Camera.main.ScreenPointToRay(Input.mousePosition));
            List<string> names = new List<string>();
            bool foundPath = false;
            foreach (RaycastHit hit in allHit)
            {
                names.Add(hit.collider.name);
                if (objNames.Contains(hit.collider.name))
                {
                    foundPath = true;
                    if (startTile == int.Parse(hit.collider.name.Split('-')[1]) && !inMaze)
                    {
                        inMaze = true;
                    }
                    else if (endTile == int.Parse(hit.collider.name.Split('-')[1]) && inMaze)
                    {
                        inMaze = false;
                        moduleSolved = true;
                        Debug.LogFormat("[Cursor Maze #{0}] Module solved", moduleId);
                        audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.CorrectChime, transform);
                        GetComponent<KMBombModule>().HandlePass();
                        return;
                    }
                }
            }
            if (!foundPath && inMaze)
            {
                inMaze = false;
                audio.PlaySoundAtTransform("exit", transform);
            }
        }
        else if (inMaze)
        {
            inMaze = false;
            audio.PlaySoundAtTransform("exit", transform);
        }
    }

    bool ValidTile(int index)
    {
        if (startTile == index)
            return false;
        int PathCt = 0;
        if ((index - 8) > -1)
        {
            if (isPath[index - 8])
                PathCt++;
        }
        if ((index + 8) < 64)
        {
            if (isPath[index + 8])
                PathCt++;
        }
        if (Modulo(index - 1, 8) != 7)
        {
            if (isPath[index - 1])
                PathCt++;
        }
        if (Modulo(index + 1, 8) != 0)
        {
            if (isPath[index + 1])
                PathCt++;
        }
        if (PathCt == 1)
            return true;
        else
            return false;
    }

    int Modulo(int x, int m)
    {
        return (x % m + m) % m;
    }
}