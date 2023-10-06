using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

public class CursorMazeScript : MonoBehaviour {

    public KMAudio audio;
    public KMColorblindMode colorblind;
    public KMSelectable moduleSelectable;
    public GameObject cbTextPrefab;
    public GameObject[] tileColliders;
    public GameObject fakeButton;
    public GameObject tpCursor;
    public Material[] materials;
    private RaycastHit[] allHit;

    private string[] objNames = new string[64];
    private bool[] isPath;
    private string tpCorrectPath;
    private string tpCurrentMoves = string.Empty;
    private bool focused = false;
    private bool inMaze = false;
    private bool tpActive = false;
    private bool tpMove = true;
    private int startTile = -1;
    private int endTile = -1;
    private int tpTile = -1;

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
        GetComponent<KMBombModule>().OnActivate += OnActivate;
    }

    void Start() {
        fakeButton.SetActive(false);
        redo:
        isPath = new bool[64];
        tpCorrectPath = string.Empty;
        int index = Random.Range(0, objNames.Length);
        startTile = index;
        isPath[index] = true;
        int pathMax = Random.Range(25, 41);
        int pathCount = 0;
        while (pathCount != pathMax)
        {
            List<int> possibleTiles = new List<int>();
            List<char> possibleMoves = new List<char>();
            if ((index - 8) > -1)
            {
                if (ValidTile(index - 8))
                {
                    possibleTiles.Add(index - 8);
                    possibleMoves.Add('u');
                }
            }
            if ((index + 8) < 64)
            {
                if (ValidTile(index + 8))
                {
                    possibleTiles.Add(index + 8);
                    possibleMoves.Add('d');
                }
            }
            if (Modulo(index - 1, 8) != 7)
            {
                if (ValidTile(index - 1))
                {
                    possibleTiles.Add(index - 1);
                    possibleMoves.Add('l');
                }
            }
            if (Modulo(index + 1, 8) != 0)
            {
                if (ValidTile(index + 1))
                {
                    possibleTiles.Add(index + 1);
                    possibleMoves.Add('r');
                }
            }
            if (possibleTiles.Count == 0)
                goto redo;
            int choice = Random.Range(0, possibleTiles.Count);
            index = possibleTiles[choice];
            tpCorrectPath += possibleMoves[choice];
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
                    GameObject cbText = Instantiate(cbTextPrefab, tileColliders[i * 8 + j].transform);
                    if (colorblind.ColorblindModeActive)
                        cbText.GetComponent<TextMesh>().text = "G";
                    logLine += "G";
                }
                else if (endTile == (i * 8 + j))
                {
                    tileColliders[i * 8 + j].GetComponent<Renderer>().material = materials[1];
                    GameObject cbText = Instantiate(cbTextPrefab, tileColliders[i * 8 + j].transform);
                    if (colorblind.ColorblindModeActive)
                        cbText.GetComponent<TextMesh>().text = "R";
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
            if (tpActive)
                allHit = Physics.RaycastAll(new Ray(tpCursor.transform.position, -transform.up));
            else
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
                if (tpActive)
                {
                    StartCoroutine(MoveCursor(TPCursorPosFix(tileColliders[startTile].transform.localPosition)));
                    tpTile = -1;
                    tpCurrentMoves = string.Empty;
                }
            }
        }
        else if (inMaze)
        {
            inMaze = false;
            audio.PlaySoundAtTransform("exit", transform);
            if (tpActive)
            {
                StartCoroutine(MoveCursor(TPCursorPosFix(tileColliders[startTile].transform.localPosition)));
                tpTile = startTile;
                tpCurrentMoves = string.Empty;
            }
        }
    }

    void OnActivate()
    {
        if (TwitchPlaysActive)
        {
            tpCursor.transform.localPosition = TPCursorPosFix(tileColliders[startTile].transform.localPosition);
            tpCursor.SetActive(true);
            tpTile = startTile;
            tpActive = true;
        }
    }

    bool CursorInPosition(Vector3 target)
    {
        if (!(Vector3.Distance(TPCursorPosFix(tpCursor.transform.localPosition), target) < 0.0001f))
        {
            return false;
        }
        return true;
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

    Vector3 TPCursorPosFix(Vector3 posToFix)
    {
        return new Vector3(posToFix.x, 0.0006f, posToFix.z);
    }

    IEnumerator MoveCursor(Vector3 target)
    {
        while (!tpMove) yield return null;
        tpMove = false;
        Vector3 startPos = TPCursorPosFix(tpCursor.transform.localPosition);
        float t = 0f;
        while (!CursorInPosition(target))
        {
            tpCursor.transform.localPosition = Vector3.Lerp(startPos, target, t);
            t += Time.deltaTime * 3f;
            yield return null;
        }
        tpMove = true;
        if (tpTile == -1)
            tpTile = startTile;
    }

    //twitch plays
    #pragma warning disable 414
    private readonly string TwitchHelpMessage = @"!{0} move <u/d/l/r> [Moves the cursor in the specified direction] | Moves may be chained, for ex: !{0} move udlr | On Twitch Plays a fake cursor will be placed on the green tile and it will return to the tile upon deselecting the module or touching a wall";
    #pragma warning restore 414
    private bool TwitchPlaysActive;
    IEnumerator ProcessTwitchCommand(string command)
    {
        string[] parameters = command.Split(' ');
        if (Regex.IsMatch(parameters[0], @"^\s*move\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            yield return null;
            if (parameters.Length > 2)
            {
                yield return "sendtochaterror Too many parameters!";
            }
            else if (parameters.Length == 2)
            {
                char[] dirs = { 'u', 'd', 'l', 'r' };
                for (int i = 0; i < parameters[1].Length; i++)
                {
                    if (!dirs.Contains(parameters[1].ToLower()[i]))
                    {
                        yield return "sendtochaterror!f The specified direction '" + parameters[1][i] + "' is invalid!";
                        yield break;
                    }
                }
                for (int i = 0; i < parameters[1].Length; i++)
                {
                    while (!tpMove) { yield return "trycancel Halted movement of the cursor due to a request to cancel!"; if (tpTile == -1) yield break; }
                    if (parameters[1].ToLower()[i].Equals('u'))
                    {
                        if (tpTile - 8 < 0)
                            StartCoroutine(MoveCursor(new Vector3(tileColliders[tpTile].transform.localPosition.x, 0.0006f, tileColliders[tpTile].transform.localPosition.z + 0.0125f)));
                        else
                            StartCoroutine(MoveCursor(TPCursorPosFix(tileColliders[tpTile - 8].transform.localPosition)));
                        tpCurrentMoves += "u";
                        tpTile -= 8;
                    }
                    else if (parameters[1].ToLower()[i].Equals('d'))
                    {
                        if (tpTile + 8 > 63)
                            StartCoroutine(MoveCursor(new Vector3(tileColliders[tpTile].transform.localPosition.x, 0.0006f, tileColliders[tpTile].transform.localPosition.z - 0.0125f)));
                        else
                            StartCoroutine(MoveCursor(TPCursorPosFix(tileColliders[tpTile + 8].transform.localPosition)));
                        tpCurrentMoves += "d";
                        tpTile += 8;
                    }
                    else if (parameters[1].ToLower()[i].Equals('l'))
                    {
                        if (tpTile == 0 || tpTile == 8 || tpTile == 16 || tpTile == 24 || tpTile == 32 || tpTile == 40 || tpTile == 48 || tpTile == 56)
                            StartCoroutine(MoveCursor(new Vector3(tileColliders[tpTile].transform.localPosition.x - 0.0125f, 0.0006f, tileColliders[tpTile].transform.localPosition.z)));
                        else
                            StartCoroutine(MoveCursor(TPCursorPosFix(tileColliders[tpTile - 1].transform.localPosition)));
                        tpCurrentMoves += "l";
                        tpTile--;
                    }
                    else
                    {
                        if (tpTile == 7 || tpTile == 15 || tpTile == 23 || tpTile == 31 || tpTile == 39 || tpTile == 47 || tpTile == 55 || tpTile == 63)
                            StartCoroutine(MoveCursor(new Vector3(tileColliders[tpTile].transform.localPosition.x + 0.0125f, 0.0006f, tileColliders[tpTile].transform.localPosition.z)));
                        else
                            StartCoroutine(MoveCursor(TPCursorPosFix(tileColliders[tpTile + 1].transform.localPosition)));
                        tpCurrentMoves += "r";
                        tpTile++;
                    }
                    if (tpTile == endTile)
                        yield return "solve";
                }
            }
            else if (parameters.Length == 1)
            {
                yield return "sendtochaterror Please specify at least one direction to move in!";
            }
        }
    }

    IEnumerator TwitchHandleForcedSolve()
    {
        while (tpTile == -1 || !tpMove) yield return null;
        moduleSelectable.OnDefocus = null;
        focused = true;
        if (!tpCorrectPath.Equals(tpCurrentMoves))
            yield return ProcessTwitchCommand("move " + tpCorrectPath.Substring(tpCurrentMoves.Length));
        while (!moduleSolved) yield return null;
    }
}