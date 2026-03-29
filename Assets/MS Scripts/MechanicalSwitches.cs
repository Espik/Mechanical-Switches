using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KModkit;

/* Known bugs/issues:
 * 
 * Check if four rotations will always rotate the key back to the same orientation
 * There is a tiny window where you can hold the keys even when the module will strike
 * The module always throws an exception when it loads
 */

public class MechanicalSwitches : MonoBehaviour {
    public KMAudio Audio;
    public KMBombInfo Bomb;
    public KMBombModule Module;

    public KMSelectable[] KeySelectables;
    public Transform[] KeyTransforms;

    public Color[] SwitchColors;
    public Material[] SwitchMaterials;

    public MeshRenderer[] SwitchStems;
    public TextMesh[] StemColorblindTexts;

    public MeshRenderer[] KeyLights;
    public Light[] KeyPointLights;

    public KMColorblindMode ColorblindMode;
    public TextMesh[] KeyColorblindTexts;

    // Hardcoded info
    private readonly string[] KEY_LABELS = { "X", "B", "C", "R", "L" };
    private readonly int ITERATION_COUNT = 3;

    private readonly float[] KEY_X_POS = { -0.0575f, -0.0275f, 0.0025f, 0.0325f, 0.0625f };
    private readonly float KEY_Y_POS = 0.036f;
    private readonly float KEY_Z_POS = -0.0465f;

    private readonly float DISTANCE_FACTOR = 0.0015f;

    private readonly int[] RELEASE_COLORS = { 3, 28, 17, 24, 9, 5, 16 };
    private readonly int[] RELEASE_COLOR_VALUES = { 1, 2, 3, 4, 5, 6, 10 };

    private readonly int[] HOLD_COLORS = { 9, 10, 3, 24, 28, 5, 17, 25 };
    private readonly int[] HOLD_COLOR_VALUES = { 0, 0, 2, 2, 4, 6, 7, 9 };

    private readonly char[] LETTER_NAMES = { 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z' };
    private readonly int[] LETTER_ROOTS = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 1, 2, 3, 4, 5, 6, 7, 8, 9, 1, 2, 3, 4, 5, 6, 7, 8 };
    private readonly string[] LETTER_MORSE = {
        ".-", "-...", "-.-.", "-..", ".", "..-.", "--.", "....", "..", ".---", "-.-", ".-..", "--",
        "-.", "---", ".--.", "--.-", ".-.", "...", "-", "..-", "...-", ".--", "-..-", "-.--", "--.." };

    // Solving info
    private MechanicalKey[] keys = new MechanicalKey[5];
    private KeySwitch[] switches = new KeySwitch[34];

    private int[][] grid = new int[9][];
    private int[][] keyPos = new int[9][];
    private int[] convertedSerialNumber = new int[6];

    private int[] holdOrder = new int[5];
    private int keysComplete = 0;

    private bool isSunday = false;
    private bool emptyPortPlate = false;

    private bool colorblind = false;

    // Key info
    private float[] keyDistances = new float[5];
    private byte[] keyReachedEnd = new byte[5];

    private bool[] keyPressed = new bool[5];
    private bool[] keyRegistered = new bool[5];
    
    private float[] keyPressTimes = new float[5];
    private uint[] keyPressCount = new uint[5];

    private int[] holdCondition = new int[5];
    private int[] releaseCondition = new int[5];

    private bool canPress = false;
    private byte animationStep = 255;

    private bool willStrike = false;
    private string strikeReason = "";

    private bool[] willFault = new bool[5];
    private int faultyKey = -1;
    private int currentFault = 0;
    private float faultTime = 0;

    private List<float> rapidTapTimes = new List<float>();

    // Logging info
    private static int moduleIdCounter = 1;
    private int moduleId;
    private bool moduleSolved = false;
    private bool canAutoSolve = false;

    private int attempt = 0;


    // Ran as bomb loads
    private void Awake() {
        moduleId = moduleIdCounter++;

        // Delegation
        for (int i = 0; i < KeySelectables.Length; i++) {
            int j = i;

            KeySelectables[i].OnInteract += delegate () {
                PressKey(j);
                return false;
            };

            KeySelectables[i].OnInteractEnded += delegate () {
                ReleaseKey(j);
            };
        }
    }

    // Gets module ready
    private void Start() {
        InitSwitches();
        ConvertSerialNumber();
        InitGrid();
        ResetKeyVariables();

        Debug.LogFormat("[Mechanical Switches #{0}] The converted serial number digits are: {1}, {2}, {3}, {4}, {5}, {6}",
            moduleId, convertedSerialNumber[0], convertedSerialNumber[1], convertedSerialNumber[2],
            convertedSerialNumber[3], convertedSerialNumber[4], convertedSerialNumber[5]);

        for (int i = 0; i < KeyPointLights.Length; i++)
            KeyPointLights[i].range *= transform.lossyScale.x;

        // Various rules for the List of Rules
        isSunday = DateTime.Now.DayOfWeek.ToString() == "Sunday";

        foreach (object[] plate in Bomb.GetPortPlates()) {
            if (plate.Length == 0) {
                emptyPortPlate = true;
                break;
            }
        }

        colorblind = ColorblindMode.ColorblindModeActive;

        if (colorblind) {
            for (int i = 0; i < KeyColorblindTexts.Length; i++)
                KeyColorblindTexts[i].text = GetColorblindText(34);
        }

        // Temporary puts the keys all the way down to make the start animation faster
        for (int i = 0; i < keyDistances.Length; i++)
            keyDistances[i] = 4.0f;

        StartCoroutine(ResetModule());
    }

    // Resets the module
    private IEnumerator ResetModule() {
        attempt++;
        Debug.LogFormat("[Mechanical Switches #{0}] Attempt {1}:", moduleId, attempt);

        animationStep = 0; // Presses the keys down
        while (animationStep < 1)
            yield return null;

        SetFaultyKey(); // Makes one of the keys faulty
        SetSwitches(); // Assigns the switches to the keys
        NumberKeys(); // Numbers the keys based on their switches

        var keyOrder = GetKeyOrder(); // Gets the key order from Table 2
        Debug.LogFormat("[Mechanical Switches #{0}] The order obtained from Table 2 is: {1}, {2}, {3}, {4}, {5}.",
            moduleId, keyOrder[0], keyOrder[1], keyOrder[2], keyOrder[3], keyOrder[4]);

        ResetGridPositions(); // Resets the positions of each key on the grid
        SetStartingPositions(keyOrder); // Sets the keys in their starting positions on the grid

        for (int i = 0; i < ITERATION_COUNT; i++) {
            Debug.LogFormat("[Mechanical Switches #{0}] Start of iteration {1}:", moduleId, i + 1);
            MoveAllKeys(); // Moves all the keys on the grid
        }

        Debug.LogFormat("[Mechanical Switches #{0}] All iterations complete. The keys are now at their ending positions.", moduleId);

        keysComplete = 0;
        holdOrder = GetHoldOrder(); // Gets the hold order for the keys
        Debug.LogFormat("[Mechanical Switches #{0}] The keys must be held in this order: {1}, {2}, {3}, {4}, {5}.",
            moduleId, KEY_LABELS[holdOrder[0]], KEY_LABELS[holdOrder[1]], KEY_LABELS[holdOrder[2]], KEY_LABELS[holdOrder[3]], KEY_LABELS[holdOrder[4]]);

        animationStep = 2; // Brings the keys up
        while (animationStep < 7)
            yield return null;

        canPress = true;
    }


    // Initializes all the switches
    private void InitSwitches() {
        switches[0] = new KeySwitch(0, 1, "Cherry Black", "Cherry", SwitchColors[0], "Black", "Linear", new[] { 60.0f, 60.0f }, 2.0f, 4.0f);
        switches[1] = new KeySwitch(1, 1, "Kailh Box Black", "Kailh Box", SwitchColors[1], "Black", "Linear", new[] { 60.0f, 60.0f }, 1.8f, 3.6f);
        switches[2] = new KeySwitch(2, 1, "Kailh Pro Burgundy", "Kailh Pro", SwitchColors[2], "Burgundy", "Linear", new[] { 50.0f, 50.0f }, 1.7f, 3.6f);
        switches[3] = new KeySwitch(3, 2, "Cherry Red", "Cherry", SwitchColors[3], "Red", "Linear", new[] { 45.0f, 45.0f }, 2.0f, 4.0f);
        switches[4] = new KeySwitch(4, 2, "Kailh Box Red", "Kailh Box", SwitchColors[4], "Red", "Linear", new[] { 45.0f, 45.0f }, 1.8f, 3.6f);
        switches[5] = new KeySwitch(5, 2, "Kailh Pro Purple", "Kailh Pro", SwitchColors[5], "Purple", "Tactile", new[] { 50.0f, 50.0f }, 1.7f, 3.6f);
        switches[6] = new KeySwitch(6, 3, "Cherry Brown", "Cherry", SwitchColors[6], "Brown", "Tactile", new[] { 45.0f, 45.0f }, 2.0f, 4.0f);
        switches[7] = new KeySwitch(7, 3, "Kailh Box Brown", "Kailh Box", SwitchColors[7], "Brown", "Tactile", new[] { 60.0f, 60.0f }, 1.8f, 3.6f);
        switches[8] = new KeySwitch(8, 3, "Kailh Pro Green", "Kailh Pro", SwitchColors[8], "Green", "Clicky", new[] { 50.0f, 50.0f }, 1.7f, 3.6f);
        switches[9] = new KeySwitch(9, 4, "Cherry Blue", "Cherry", SwitchColors[9], "Blue", "Clicky", new[] { 50.0f, 50.0f }, 2.2f, 4.0f);
        switches[10] = new KeySwitch(10, 4, "Kailh Box White", "Kailh Box", SwitchColors[10], "White", "Clicky", new[] { 55.0f, 55.0f }, 1.8f, 3.6f);
        switches[11] = new KeySwitch(11, 4, "Kailh Speed Silver", "Kailh", SwitchColors[11], "Silver", "Linear", new[] { 50.0f, 50.0f }, 1.1f, 3.5f);
        switches[12] = new KeySwitch(12, 5, "Cherry Clear", "Cherry", SwitchColors[12], "Clear", "Tactile", new[] { 65.0f, 65.0f }, 2.0f, 4.0f);
        switches[13] = new KeySwitch(13, 5, "Gateron Clear", "Gateron", SwitchColors[13], "Clear", "Linear", new[] { 35.0f, 35.0f }, 2.0f, 4.0f);
        switches[14] = new KeySwitch(14, 5, "Kailh Box Navy", "Kailh Box", SwitchColors[14], "Navy", "Clicky", new[] { 75.0f, 75.0f }, 1.7f, 3.6f);
        switches[15] = new KeySwitch(15, 5, "Kailh Speed Copper", "Kailh", SwitchColors[15], "Copper", "Tactile", new[] { 50.0f, 50.0f }, 1.1f, 3.5f);
        switches[16] = new KeySwitch(16, 6, "Cherry White", "Cherry", SwitchColors[16], "White", "Clicky", new[] { 85.0f, 85.0f }, 2.0f, 4.0f);
        switches[17] = new KeySwitch(17, 6, "Gateron Yellow", "Gateron", SwitchColors[17], "Yellow", "Linear", new[] { 50.0f, 50.0f }, 2.0f, 4.0f);
        switches[18] = new KeySwitch(18, 6, "Kailh Box Jade", "Kailh Box", SwitchColors[18], "Jade", "Clicky", new[] { 65.0f, 65.0f }, 1.7f, 3.6f);
        switches[19] = new KeySwitch(19, 6, "Kailh Speed Bronze", "Kailh", SwitchColors[19], "Bronze", "Clicky", new[] { 50.0f, 50.0f }, 1.1f, 3.5f);
        switches[20] = new KeySwitch(20, 7, "Cherry Green", "Cherry", SwitchColors[20], "Green", "Tactile", new[] { 80.0f, 80.0f }, 2.2f, 4.0f);
        switches[21] = new KeySwitch(21, 7, "Gateron Green", "Gateron", SwitchColors[21], "Green", "Clicky", new[] { 80.0f, 80.0f }, 2.0f, 4.0f);
        switches[22] = new KeySwitch(22, 7, "Kailh Box Dark Yellow", "Kailh Box", SwitchColors[22], "Dark Yellow", "Linear", new[] { 70.0f, 70.0f }, 1.8f, 3.6f);
        switches[23] = new KeySwitch(23, 7, "Kailh Speed Gold", "Kailh", SwitchColors[23], "Gold", "Clicky", new[] { 50.0f, 50.0f }, 1.4f, 3.5f);
        switches[24] = new KeySwitch(24, 7, "Razer Green", "Razer", SwitchColors[24], "Green", "Clicky", new[] { 50.0f, 50.0f }, 1.9f, 4.0f);
        switches[25] = new KeySwitch(25, 8, "Cherry Grey (Linear)", "Cherry", SwitchColors[25], "Grey", "Linear", new[] { 80.0f, 80.0f }, 2.0f, 4.0f);
        switches[26] = new KeySwitch(26, 8, "Gateron Tealios", "Gateron", SwitchColors[26], "Tealios", "Linear", new[] { 67.0f, 67.0f }, 0.1f, 0.1f);
        switches[27] = new KeySwitch(27, 8, "Kailh Box Burnt Orange", "Kailh Box", SwitchColors[27], "Burnt Orange", "Tactile", new[] { 70.0f, 70.0f }, 1.8f, 3.6f);
        switches[28] = new KeySwitch(28, 8, "Razer Orange", "Razer", SwitchColors[28], "Orange", "Silent Linear", new[] { 45.0f, 45.0f }, 1.9f, 4.0f);
        switches[29] = new KeySwitch(29, 9, "Cherry Grey (Tactile)", "Cherry", SwitchColors[29], "Grey", "Tactile", new[] { 80.0f, 80.0f }, 2.0f, 4.0f);
        switches[30] = new KeySwitch(30, 9, "Gateron Zealios", "Gateron", SwitchColors[30], "Zealios", "Tactile", new[] { 62.0f, 78.0f }, 0.1f, 0.1f);
        switches[31] = new KeySwitch(31, 9, "Gateron Aliaz", "Gateron", SwitchColors[31], "Aliaz", "Silent Tactile", new[] { 60.0f, 100.0f }, 2.0f, 4.0f);
        switches[32] = new KeySwitch(32, 9, "Kailh Box Pale Blue", "Kailh Box", SwitchColors[32], "Pale Blue", "Clicky", new[] { 70.0f, 70.0f }, 1.8f, 3.6f);
        switches[33] = new KeySwitch(33, 9, "Razer Yellow", "Razer", SwitchColors[33], "Yellow", "Linear", new[] { 45.0f, 45.0f }, 1.2f, 3.5f);
    }

    // Converts the serial number characters to their proper numbers
    private void ConvertSerialNumber() {
        var serialNumber = Bomb.GetSerialNumber();

        for (int i = 0; i < serialNumber.Length; i++) {
            switch (serialNumber[i]) {
                case 'A':
                case 'J':
                case 'S':
                    convertedSerialNumber[i] = 1;
                    break;
                case 'B':
                case 'K':
                case 'T':
                    convertedSerialNumber[i] = 2;
                    break;
                case 'C':
                case 'L':
                case 'U':
                    convertedSerialNumber[i] = 3;
                    break;
                case 'D':
                case 'M':
                case 'V':
                    convertedSerialNumber[i] = 4;
                    break;
                case 'E':
                case 'N':
                case 'W':
                    convertedSerialNumber[i] = 5;
                    break;
                case 'F':
                case 'O':
                case 'X':
                    convertedSerialNumber[i] = 6;
                    break;
                case 'G':
                case 'P':
                case 'Y':
                    convertedSerialNumber[i] = 7;
                    break;
                case 'H':
                case 'Q':
                case 'Z':
                    convertedSerialNumber[i] = 8;
                    break;
                case 'I':
                case 'R':
                    convertedSerialNumber[i] = 9;
                    break;
                default:
                    convertedSerialNumber[i] = int.Parse(serialNumber[i].ToString());
                    break;
            }
        }
    }

    // Initializes the grid
    private void InitGrid() {
        for (int i = 0; i < grid.Length; i++) {
            grid[i] = new int[9];
            keyPos[i] = new int[9];

            for (int j = 0; j < grid[i].Length; j++)
                grid[i][j] = 0;
        }

        grid[0][0] = 32;
        grid[0][2] = 9;
        grid[0][4] = 40;
        grid[0][6] = 2;
        grid[0][8] = 11;

        grid[1][1] = 8;
        grid[1][3] = 12;
        grid[1][5] = 4;
        grid[1][7] = 41;

        grid[2][0] = 15;
        grid[2][2] = 39;
        grid[2][4] = 25;
        grid[2][6] = 30;
        grid[2][8] = 35;

        grid[3][1] = 20;
        grid[3][3] = 18;
        grid[3][5] = 22;
        grid[3][7] = 17;

        grid[4][0] = 19;
        grid[4][2] = 31;
        grid[4][4] = 26;
        grid[4][6] = 37;
        grid[4][8] = 24;

        grid[5][1] = 36;
        grid[5][3] = 7;
        grid[5][5] = 5;
        grid[5][7] = 33;

        grid[6][0] = 16;
        grid[6][2] = 38;
        grid[6][4] = 27;
        grid[6][6] = 21;
        grid[6][8] = 1;

        grid[7][1] = 3;
        grid[7][3] = 13;
        grid[7][5] = 28;
        grid[7][7] = 10;

        grid[8][0] = 29;
        grid[8][2] = 6;
        grid[8][4] = 14;
        grid[8][6] = 34;
        grid[8][8] = 23;
    }

    // Resets the variables for all the key models
    private void ResetKeyVariables() {
        for (int i = 0; i < KeyTransforms.Length; i++) {
            keyDistances[i] = 0.0f;
            keyReachedEnd[i] = 0;

            keyPressed[i] = false;
            keyRegistered[i] = false;

            keyPressTimes[i] = 0;
            keyPressCount[i] = 0;

            releaseCondition[i] = -1;
            holdCondition[i] = -1;
        }
    }


    // Makes one of the keys faulty
    private void SetFaultyKey() {
        faultyKey = -1;
        currentFault = 0;
        faultTime = 0.0f;

        rapidTapTimes.Clear();

        for (int i = 0; i < willFault.Length; i++)
            willFault[i] = false;

        var rand = UnityEngine.Random.Range(0, keys.Length);
        willFault[rand] = true;
        Debug.LogFormat("<Mechanical Switches #{0}> Key {1} will be faulty.", moduleId, KEY_LABELS[rand]);

        for (int i = 0; i < KeyLights.Length; i++)
            SetKeyLight(i, 34);
    }

    // Assigns the switches to the keys
    private void SetSwitches() {
        for (int i = 0; i < keys.Length; i++) {
            var rand = UnityEngine.Random.Range(0, switches.Length);
            keys[i] = new MechanicalKey(i, switches[rand]);
            Debug.LogFormat("[Mechanical Switches #{0}] Key {1} has a {2} switch.", moduleId, KEY_LABELS[i], switches[rand].GetName());

            if (switches[rand].GetColorName() == "Clear") {
                SwitchStems[i * 2].material = SwitchMaterials[1];
                SwitchStems[i * 2 + 1].material = SwitchMaterials[1];
            }

            else {
                SwitchStems[i * 2].material = SwitchMaterials[0];
                SwitchStems[i * 2 + 1].material = SwitchMaterials[0];
            }

            SwitchStems[i * 2].material.color = SwitchColors[rand];
            SwitchStems[i * 2 + 1].material.color = SwitchColors[rand];

            if (SwitchColors[rand].r + SwitchColors[rand].g + SwitchColors[rand].b >= 1.5f) {
                StemColorblindTexts[i * 2].color = new Color(0.0f, 0.0f, 0.0f);
                StemColorblindTexts[i * 2 + 1].color = new Color(0.0f, 0.0f, 0.0f);
            }

            else {
                StemColorblindTexts[i * 2].color = new Color(1.0f, 1.0f, 1.0f);
                StemColorblindTexts[i * 2 + 1].color = new Color(1.0f, 1.0f, 1.0f);
            }

            if (colorblind) {
                StemColorblindTexts[i * 2].text = GetColorblindText(rand)[0].ToString();
                StemColorblindTexts[i * 2 + 1].text = GetColorblindText(rand)[1].ToString();
            }
        }
    }

    // Numbers the keys based on their switches
    private void NumberKeys() {
        var keyPos = new int[5];
        var switchNos = new int[5];

        for (int i = 0; i < keyPos.Length; i++) {
            keyPos[i] = i;
            switchNos[i] = keys[i].GetKeySwitch().GetId();
        }

        // Insertion sort - https://www.geeksforgeeks.org/dsa/insertion-sort-algorithm/
        for (int i = 1; i < keys.Length; i++) {
            var pivotSwitch = switchNos[i];
            var pivotKey = keyPos[i];

            int j = i - 1;

            while (j >= 0 && switchNos[j] > pivotSwitch) {
                switchNos[j + 1] = switchNos[j];
                keyPos[j + 1] = keyPos[j];

                j--;
            }

            switchNos[j + 1] = pivotSwitch;
            keyPos[j + 1] = pivotKey;
        }

        for (int i = 0; i < keyPos.Length; i++)
            keys[keyPos[i]].SetNumber(i + 1);

        for (int i = 0; i < keys.Length; i++)
            Debug.LogFormat("[Mechanical Switches #{0}] Key {1} is numbered {2}.", moduleId, KEY_LABELS[i], keys[i].GetNumber());
    }

    // Gets the key order from Table 2
    private int[] GetKeyOrder() {
        var cherryCount = 0;
        var gateronCount = 0;
        var kailhBoxCount = 0;
        var kailhProCount = 0;
        var razerCount = 0;

        var blueCount = 0;
        var brownCount = 0;
        var greenCount = 0;
        var redCount = 0;
        var tealiosCount = 0;
        var whiteCount = 0;
        var zealiosCount = 0;

        var speedCount = 0;
        var categoriesPresent = new[] { false, false, false, false, false };

        for (int i = 0; i < keys.Length; i++) {
            switch (keys[i].GetKeySwitch().GetBrand()) {
                case "Cherry":
                    cherryCount++;
                    break;
                case "Gateron":
                    gateronCount++;
                    break;
                case "Kailh Box":
                    kailhBoxCount++;
                    break;
                case "Kailh Pro":
                    kailhProCount++;
                    break;
                case "Razer":
                    razerCount++;
                    break;
            }

            switch (keys[i].GetKeySwitch().GetColorName()) {
                case "Blue":
                case "Pale Blue":
                    blueCount++;
                    break;
                case "Brown":
                    brownCount++;
                    break;
                case "Green":
                    greenCount++;
                    break;
                case "Red":
                    redCount++;
                    break;
                case "Tealios":
                    tealiosCount++;
                    break;
                case "White":
                    whiteCount++;
                    break;
                case "Zealios":
                    zealiosCount++;
                    break;

                case "Silver":
                case "Copper":
                case "Bronze":
                case "Gold":
                    speedCount++;
                    break;
            }

            switch (keys[i].GetKeySwitch().GetCategory()) {
                case "Clicky":
                    categoriesPresent[0] = true;
                    break;
                case "Tactile":
                    categoriesPresent[1] = true;
                    break;
                case "Linear":
                    categoriesPresent[2] = true;
                    break;
                case "Silent Tactile":
                    categoriesPresent[3] = true;
                    break;
                case "Silent Linear":
                    categoriesPresent[4] = true;
                    break;
            }
        }

        if (cherryCount == 5)
            return new[] { 1, 2, 3, 4, 5 };
        else if (redCount == 2)
            return new[] { 2, 4, 5, 3, 1 };
        else if (blueCount == 3)
            return new[] { 1, 4, 5, 2, 3 };
        else if (speedCount == 1)
            return new[] { 5, 2, 1, 4, 3 };
        else if (brownCount == 4)
            return new[] { 4, 1, 2, 3, 5, };
        else if (razerCount == 2)
            return new[] { 2, 3, 5, 4, 1, };
        else if (whiteCount == 1)
            return new[] { 4, 1, 5, 2, 3, };
        else if (kailhBoxCount == 3)
            return new[] { 1, 5, 2, 3, 4 };
        else if (greenCount == 3)
            return new[] { 2, 5, 3, 4, 1, };
        else if (razerCount > 2)
            return new[] { 3, 4, 1, 5, 2 };
        else if (gateronCount + kailhProCount > 2)
            return new[] { 2, 3, 1, 4, 5 };
        else if (gateronCount == 1)
            return new[] { 4, 3, 5, 1, 2 };
        else if (kailhBoxCount == 5)
            return new[] { 3, 1, 2, 4, 5 };
        else if (gateronCount == 4)
            return new[] { 2, 4, 1, 3, 5 };
        else if (tealiosCount > 0 && zealiosCount > 0)
            return new[] { 4, 2, 3, 1, 5 };
        else if (!categoriesPresent.Contains(false))
            return new[] { 4, 2, 3, 1, 5, };
        else
            return new[] { 5, 4, 3, 2, 1 };
    }

    // Resets the positions of each key on the grid
    private void ResetGridPositions() {
        for (int i = 0; i < keyPos.Length; i++) {
            for (int j = 0; j < keyPos[i].Length; j++)
                keyPos[i][j] = -1;
        }
    }

    // Sets the keys in their starting positions on the grid
    private void SetStartingPositions(int[] order) {
        for (int i = 0; i < order.Length; i++) {
            for (int j = 0; j < keys.Length; j++) {
                if (keys[j].GetNumber() == order[i]) {
                    keyPos[0][i * 2] = j;
                    keys[j].SetTablePosition(new[] { 0, i * 2 });
                    break;
                }
            }
        }
    }

    // Moves all the keys on the grid
    private void MoveAllKeys() {
        var snIndex = -1;
        var keyIndex = -1;

        for (int i = 1; i <= 5; i++) {
            // Finds the key with the right number
            for (int j = 0; j < keys.Length; j++) {
                if (keys[j].GetNumber() == i) {
                    keyIndex = j;
                    break;
                }
            }

            // Moves each key an amount of times equal to its number
            var keyStuck = false;

            for (int j = 0; j < i; j++) {
                // Attempt to move a key
                snIndex++;
                snIndex %= 6;

                if (!keyStuck) {
                    var movementAttempts = 0;
                    var canMove = false;

                    var currentPos = keys[keyIndex].GetTablePosition();
                    var rotation = keys[keyIndex].GetRotation();
                    var validPositions = GetValidPositions(currentPos);

                    // Check if the key can move
                    do {
                        movementAttempts++;
                        var desiredDirection = GetDesiredDirection(convertedSerialNumber[snIndex]) + rotation;
                        desiredDirection %= 4;

                        if (!validPositions.Contains(true)) {
                            Debug.LogFormat("[Mechanical Switches #{0}] Key number {1} cannot move in any diagonal direction. The key will stop moving for this iteration.", moduleId, keys[keyIndex].GetNumber());
                            keyStuck = true;
                        }

                        else if (!validPositions[(rotation + 2) % 4] && !validPositions[(rotation + 3) % 4]) {
                            Debug.LogFormat("[Mechanical Switches #{0}] Key number {1} cannot move downwards in either horizontal direction.", moduleId, keys[keyIndex].GetNumber());

                            if (CheckRule(grid[currentPos[0]][currentPos[1]], keyIndex)) {
                                rotation ++;
                                rotation %= 4;

                                keys[keyIndex].SetRotation(rotation);
                                Debug.LogFormat("[Mechanical Switches #{0}] Rule number {1} is true. Rotating key number {2} 90° clockwise. The key now faces {3}.",
                                    moduleId, grid[currentPos[0]][currentPos[1]], keys[keyIndex].GetNumber(), LogRotation(rotation));
                            }

                            else {
                                rotation += 2;
                                rotation %= 4;

                                keys[keyIndex].SetRotation(rotation);
                                Debug.LogFormat("[Mechanical Switches #{0}] Rule number {1} is false. Rotating key number {2} 180°. The key now faces {3}.",
                                    moduleId, grid[currentPos[0]][currentPos[1]], keys[keyIndex].GetNumber(), LogRotation(rotation));
                            }
                        }

                        else if (!validPositions[desiredDirection]) {
                            Debug.LogFormat("[Mechanical Switches #{0}] Key number {1} cannot move in its desired direction.", moduleId, keys[keyIndex].GetNumber());
                            bool invalidGridPos = false;

                            switch (desiredDirection) {
                                case 0: // Northwest
                                    if (currentPos[0] - 1 < 0 || currentPos[1] - 1 < 0)
                                        invalidGridPos = true;
                                    
                                    break;

                                case 1: // Northeast
                                    if (currentPos[0] - 1 < 0 || currentPos[1] + 1 > 8)
                                        invalidGridPos = true;

                                    break;

                                case 2: // Southeast
                                    if (currentPos[0] + 1 > 8 || currentPos[1] + 1 > 8)
                                        invalidGridPos = true;

                                    break;

                                case 3: // Southwest
                                    if (currentPos[0] + 1 > 8 || currentPos[1] - 1 < 0)
                                        invalidGridPos = true;

                                    break;

                                default:
                                    Debug.LogFormat("<Mechanical Switches #{0}> Invalid desired direction: {1}", moduleId, desiredDirection);
                                    ErrorFound();
                                    invalidGridPos = true;
                                    break;
                            }

                            if (invalidGridPos) {
                                if (Math.Abs(desiredDirection - rotation) % 2 == 0) { // Down-right
                                    rotation++;
                                    rotation %= 4;

                                    keys[keyIndex].SetRotation(rotation);
                                    Debug.LogFormat("[Mechanical Switches #{0}] The desired direction was down-right and off the table. Rotating key number {1} 90° clockwise. The key now faces {2}.",
                                        moduleId, keys[keyIndex].GetNumber(), LogRotation(rotation));
                                }

                                else { // Down-left
                                    rotation += 3;
                                    rotation %= 4;

                                    keys[keyIndex].SetRotation(rotation);
                                    Debug.LogFormat("[Mechanical Switches #{0}] The desired direction was down-left and off the table. Rotating key number {1} 90° counter-clockwise. The key now faces {2}.",
                                        moduleId, keys[keyIndex].GetNumber(), LogRotation(rotation));
                                }
                            }

                            else {
                                rotation += 3;
                                rotation %= 4;

                                keys[keyIndex].SetRotation(rotation);
                                Debug.LogFormat("[Mechanical Switches #{0}] Another key is present on the space in the desired direction. Rotating key number {1} 90° counter-clockwise. The key now faces {2}.",
                                    moduleId, keys[keyIndex].GetNumber(), LogRotation(rotation));
                            }
                        }

                        else
                            canMove = true;

                        // Can the key still move after it was rotated
                        if (!canMove && !keyStuck) {
                            desiredDirection = GetDesiredDirection(convertedSerialNumber[snIndex]) + rotation;
                            desiredDirection %= 4;

                            if (validPositions[desiredDirection]) {
                                canMove = true;
                                Debug.LogFormat("[Mechanical Switches #{0}] Key number {1} can now move in its desired direction.", moduleId, keys[keyIndex].GetNumber());
                            }

                            else
                                Debug.LogFormat("[Mechanical Switches #{0}] Key number {1} still cannot move in its desired direction. Total attempts: {2}.",
                                    moduleId, keys[keyIndex].GetNumber(), movementAttempts);
                        }

                    } while (!canMove && !keyStuck && movementAttempts < 4);

                    if (movementAttempts >= 4 && !canMove) {
                        Debug.LogFormat("[Mechanical Switches #{0}] Key number {1} could not move after four attempts. The key will stop moving for this iteration.", moduleId, keys[keyIndex].GetNumber());
                        keyStuck = true;
                    }

                    // Moves the key
                    if (canMove) {
                        var desiredDirection = GetDesiredDirection(convertedSerialNumber[snIndex]) + rotation;
                        desiredDirection %= 4;

                        keyPos[currentPos[0]][currentPos[1]] = -1;

                        switch (desiredDirection) {
                            case 0: // Northwest
                                currentPos[0]--;
                                currentPos[1]--;
                                break;

                            case 1: // Northeast
                                currentPos[0]--;
                                currentPos[1]++;
                                break;

                            case 2: // Southeast
                                currentPos[0]++;
                                currentPos[1]++;
                                break;

                            case 3: // Southwest
                                currentPos[0]++;
                                currentPos[1]--;
                                break;
                        }

                        keyPos[currentPos[0]][currentPos[1]] = keyIndex;
                        keys[keyIndex].SetTablePosition(new[] { currentPos[0], currentPos[1] });
                        Debug.LogFormat("[Mechanical Switches #{0}] Moved key number {1} to position {2}.",
                            moduleId, keys[keyIndex].GetNumber(), LogPosition(new[] { currentPos[0], currentPos[1] }));

                        if (CheckRule(grid[currentPos[0]][currentPos[1]], keyIndex)) {
                            rotation += 3;
                            rotation %= 4;

                            keys[keyIndex].SetRotation(rotation);

                            Debug.LogFormat("[Mechanical Switches #{0}] Rule number {1} is true. Rotating key number {2} 90° counter-clockwise. The key now faces {3}.",
                                moduleId, grid[currentPos[0]][currentPos[1]], keys[keyIndex].GetNumber(), LogRotation(rotation));
                        }
                    }
                }
            }

            if (!keyStuck)
                Debug.LogFormat("[Mechanical Switches #{0}] Key number {1} has finished moving for this iteration.", moduleId, keys[keyIndex].GetNumber());
        }
    }

    // Gets the hold order for the keys
    private int[] GetHoldOrder() {
        var foundKeyList = new List<int>();

        for (int i = 0; i < keyPos.Length; i++) {
            for (int j = 0; j < keyPos[i].Length; j++) {
                if (keyPos[i][j] >= 0) {
                    var foundKey = keyPos[i][j];
                    foundKeyList.Add(foundKey);

                    Debug.LogFormat("[Mechanical Switches #{0}] Key {1} (number {2}) ended at position {3}.",
                        moduleId, KEY_LABELS[foundKey], keys[foundKey].GetNumber(), LogPosition(new[] { i, j }));

                    if (foundKeyList.Count() >= 5)
                        return foundKeyList.ToArray();
                }
            }
        }

        return foundKeyList.ToArray();
    }


    // Gets all the valid movements for the key based on its position
    private bool[] GetValidPositions(int[] pos) {
        var valid = new[] { true, true, true, true };

        // Northwest
        if (pos[0] - 1 < 0 || pos[1] - 1 < 0)
            valid[0] = false;

        else if (keyPos[pos[0] - 1][pos[1] - 1] >= 0)
            valid[0] = false;

        // Northeast
        if (pos[0] - 1 < 0 || pos[1] + 1 > 8)
            valid[1] = false;

        else if (keyPos[pos[0] - 1][pos[1] + 1] >= 0)
            valid[1] = false;

        // Southeast
        if (pos[0] + 1 > 8 || pos[1] + 1 > 8)
            valid[2] = false;

        else if (keyPos[pos[0] + 1][pos[1] + 1] >= 0)
            valid[2] = false;

        // Southwest
        if (pos[0] + 1 > 8 || pos[1] - 1 < 0)
            valid[3] = false;

        else if (keyPos[pos[0] + 1][pos[1] - 1] >= 0)
            valid[3] = false;

        return valid;
    }

    // Gets the desired direction based on the converted serial number digit
    private int GetDesiredDirection(int num) {
        if (num < 0 || num > 9) {
            Debug.LogFormat("<Mechanical Switches #{0}> Invalid converted serial number digit: {1}", moduleId, num);
            ErrorFound();
            return 3;
        }

        return num < 5 ? 3 : 2;
    }

    // Checks the rule for the grid
    private bool CheckRule(int num, int keyIndex) {
        switch (num) {
            case 1: return Bomb.GetOnIndicators().Count() >= 2;
            case 2: return Bomb.GetSerialNumberLetters().Any(x => x == 'A' || x == 'E' || x == 'I' || x == 'O' || x == 'U');
            case 3: return keys[keyIndex].GetKeySwitch().GetBrand() == "Kailh Pro";
            case 4: return Bomb.GetModuleNames().Count() % 2 == 0;
            case 5: return isSunday;
            case 6: return Bomb.GetSerialNumberNumbers().Distinct().Count() >= 3;
            case 7: return keys[keyIndex].GetKeySwitch().GetBrand() == "Cherry";
            case 8: return keys[keyIndex].GetKeySwitch().GetColorName() == "Red";
            case 9: return Bomb.GetSolvableModuleNames().Count(x => x.Contains("Simon")) >= 2;
            case 10: return Bomb.GetIndicators().Count() == 1;
            case 11: return Bomb.GetBatteryCount() % 2 == 0;
            case 12: return Bomb.GetPortCount(Port.DVI) % 2 == 1;
            case 13: return emptyPortPlate;
            case 14: return keys[keyIndex].GetKeySwitch().GetColorName() == "Blue" || keys[keyIndex].GetKeySwitch().GetColorName() == "Pale Blue";
            case 15: return Bomb.GetPortCount() == 0;
            case 16: return Bomb.GetPortPlateCount() == 0;
            case 17: return keys[keyIndex].GetKeySwitch().GetBrand() == "Kailh Box";
            case 18: return keys[keyIndex].GetKeySwitch().GetColorName() == "Clear";
            case 19: return !Bomb.GetSolvableModuleNames().Any(x => x.Contains("Simon"));
            case 20: return Bomb.GetSolvableModuleNames().Count(x => x.Contains("Mechanical Switches")) == 1;
            case 21: return Bomb.GetBatteryCount() % 2 == 1;
            case 22: return Bomb.GetSolvableModuleNames().Count(x => x.Contains("Piano Keys")) >= 1;
            case 23: return Bomb.GetSolvableModuleNames().Contains("Turn The Key") || Bomb.GetSolvableModuleNames().Contains("Forget Me Not");
            case 24: return Bomb.GetPortPlateCount() == 3;
            case 25: return keys[keyIndex].GetKeySwitch().GetBrand() == "Gateron";
            case 26: return keys[keyIndex].GetNumber() == 3 || keys[keyIndex].GetNumber() == 4;
            case 27: return keys[keyIndex].GetNumber() == 2;
            case 28: return keys[keyIndex].GetNumber() == 3;
            case 29: return keys[keyIndex].GetNumber() == 2 || keys[keyIndex].GetNumber() == 5;
            case 30: return Bomb.GetIndicators().Count() % 2 == 1;
            case 31: return Bomb.GetPortCount(Port.Parallel) % 2 == 1;
            case 32: return keys[keyIndex].GetKeySwitch().GetColorName() == "Purple" || keys[keyIndex].GetKeySwitch().GetColorName() == "Yellow" || keys[keyIndex].GetKeySwitch().GetColorName() == "Dark Yellow";
            case 33: return Bomb.GetSerialNumberNumbers().Any(x => x == '0' || x == '2' || x == '4' || x == '6' || x == '8');
            case 34: return Bomb.GetBatteryCount() == 2;
            case 35: return keys[keyIndex].GetKeySwitch().GetBrand() == "Razer";
            case 36: return Bomb.GetSerialNumberNumbers().Any(x => x == '0');
            case 37: return keys[keyIndex].GetKeySwitch().GetColorName() == "Brown" || keys[keyIndex].GetKeySwitch().GetColorName() == "Black";
            case 38: return Bomb.GetBatteryCount() == 1;
            case 39: return Bomb.GetPortCount() == 5;
            case 40: return Bomb.GetIndicators().Count() == 3;
            case 41: return Bomb.GetSolvableModuleNames().Count(x => x.Contains("Souvenir")) == 1;

            default:
                Debug.LogFormat("<Mechanical Switches #{0}> Invalid rule number: {1}", moduleId, num);
                ErrorFound();
                return false;
        }
    }


    // Gets the rotation direction for logging
    private string LogRotation(int rot) {
        switch (rot) {
            case 0: return "North";
            case 1: return "East";
            case 2: return "South";
            case 3: return "West";

            default:
                Debug.LogFormat("<Mechanical Switches #{0}> Invalid rotation: {1}", moduleId, rot);
                ErrorFound();
                return "";
        }
    }

    // Gets the grid position for logging
    private string LogPosition(int[] pos) {
        var str = "";

        switch (pos[1]) {
            case 0: str = "A"; break;
            case 1: str = "B"; break;
            case 2: str = "C"; break;
            case 3: str = "D"; break;
            case 4: str = "E"; break;
            case 5: str = "F"; break;
            case 6: str = "G"; break;
            case 7: str = "H"; break;
            case 8: str = "I"; break;

            default:
                Debug.LogFormat("<Mechanical Switches #{0}> Invalid column no: {1}", moduleId, pos[1]);
                ErrorFound();
                return "";
        }

        return str + (pos[0] + 1);
    }


    // Pressing a key
    private void PressKey(int no) {
        if (canPress) {
            keyPressed[no] = true;

            if (canAutoSolve)
                ModuleSolve();
        }
    }

    // Releasing a key
    private void ReleaseKey(int no) {
        if (canPress) {
            keyPressed[no] = false;

            if (canAutoSolve)
                ModuleSolve();
        }
    }

    // Updates the positions of the keys
    private void Update() {
        for (int i = 0; i < KeyTransforms.Length; i++) {
            // Animation press
            if (animationStep == 0) {
                keyDistances[i] = Math.Min(keyDistances[i] + 0.2f, 4.0f);

                if (keyReachedEnd[i] < 2 && keyDistances[i] == 4.0f) {
                    keyReachedEnd[i] = 2;
                    animationStep = 1;

                    if (attempt > 1) {
                        switch (keys[i].GetKeySwitch().GetCategory()) {
                            case "Silent Linear":
                            case "Silent Tactile":
                                Audio.PlaySoundAtTransform("MS_SilentLinearHold", KeySelectables[i].transform);
                                break;

                            case "Linear":
                            case "Tactile":
                                Audio.PlaySoundAtTransform("MS_LinearHold", KeySelectables[i].transform);
                                break;

                            case "Clicky":
                                Audio.PlaySoundAtTransform("MS_ClickyHold", KeySelectables[i].transform);
                                break;
                        }
                    }
                }

                else if (keyDistances[i] != keys[i].GetKeySwitch().GetTravelDistance())
                    keyReachedEnd[i] = 1;
            }

            // Animation release
            else if (animationStep >= 2 && animationStep < 7) {
                switch (keys[i].GetKeySwitch().GetCategory()) {
                    case "Linear":
                    case "Silent Linear":
                        keyDistances[i] = Math.Max(keyDistances[i] - 0.2f, 0.0f);
                        break;

                    case "Tactile":
                    case "Silent Tactile":
                    case "Clicky":
                        if (keyDistances[i] < keys[i].GetKeySwitch().GetActuation())
                            keyDistances[i] = Math.Max(keyDistances[i] - 0.1f, 0.0f);

                        else
                            keyDistances[i] = Math.Max(keyDistances[i] - 0.3f, 0.0f);

                        break;

                    default:
                        Debug.LogFormat("<Mechanical Switches #{0}> Invalid category: {1}", moduleId, keys[i].GetKeySwitch().GetCategory());
                        ErrorFound();
                        keyDistances[i] = Math.Max(keyDistances[i] - 0.2f, 0.0f);
                        break;
                }

                if (keyReachedEnd[i] > 0 && keyDistances[i] == 0.0f) {
                    keyReachedEnd[i] = 0;
                    animationStep++;

                    switch (keys[i].GetKeySwitch().GetCategory()) {
                        case "Silent Linear":
                        case "Silent Tactile":
                            Audio.PlaySoundAtTransform("MS_SilentLinearRelease", KeySelectables[i].transform);
                            break;

                        case "Linear":
                        case "Tactile":
                            Audio.PlaySoundAtTransform("MS_LinearRelease", KeySelectables[i].transform);
                            break;

                        case "Clicky":
                            Audio.PlaySoundAtTransform("MS_ClickyRelease", KeySelectables[i].transform);
                            break;
                    }
                }

                else if (keyDistances[i] != 0.0f)
                    keyReachedEnd[i] = 1;
            }

            // Proper press
            else if (keyPressed[i]) {
                switch (keys[i].GetKeySwitch().GetCategory()) {
                    case "Linear":
                    case "Silent Linear":
                        keyDistances[i] = Math.Min(keyDistances[i] + 0.2f, keys[i].GetKeySwitch().GetTravelDistance());
                        break;

                    case "Tactile":
                    case "Silent Tactile":
                    case "Clicky":
                        if (keyDistances[i] < keys[i].GetKeySwitch().GetActuation())
                            keyDistances[i] = Math.Min(keyDistances[i] + 0.1f, keys[i].GetKeySwitch().GetTravelDistance());

                        else
                            keyDistances[i] = Math.Min(keyDistances[i] + 0.3f, keys[i].GetKeySwitch().GetTravelDistance());

                        break;

                    default:
                        Debug.LogFormat("<Mechanical Switches #{0}> Invalid category: {1}", moduleId, keys[i].GetKeySwitch().GetCategory());
                        ErrorFound();
                        keyDistances[i] = Math.Min(keyDistances[i] + 0.2f, keys[i].GetKeySwitch().GetTravelDistance());
                        break;
                }

                if (!moduleSolved && !keyRegistered[i] && keyDistances[i] >= keys[i].GetKeySwitch().GetActuation()) {
                    keyRegistered[i] = true;
                    StartCoroutine(RegisterHold(i));
                }

                if (keyReachedEnd[i] < 2 && keyDistances[i] == keys[i].GetKeySwitch().GetTravelDistance()) {
                    keyReachedEnd[i] = 2;

                    switch (keys[i].GetKeySwitch().GetCategory()) {
                        case "Silent Linear":
                        case "Silent Tactile":
                            Audio.PlaySoundAtTransform("MS_SilentLinearHold", KeySelectables[i].transform);
                            break;

                        case "Linear":
                        case "Tactile":
                            Audio.PlaySoundAtTransform("MS_LinearHold", KeySelectables[i].transform);
                            break;

                        case "Clicky":
                            Audio.PlaySoundAtTransform("MS_ClickyHold", KeySelectables[i].transform);
                            break;
                    }
                }

                else if (keyDistances[i] != keys[i].GetKeySwitch().GetTravelDistance()) {
                    keyReachedEnd[i] = 1;
                    var force = keys[i].GetKeySwitch().GetForce();
                    KeySelectables[i].AddInteractionPunch(UnityEngine.Random.Range(force[0] / 100.0f, (force[1] + 1) / 100.0f));
                }
            }

            // Proper release
            else {
                switch (keys[i].GetKeySwitch().GetCategory()) {
                    case "Linear":
                    case "Silent Linear":
                        keyDistances[i] = Math.Max(keyDistances[i] - 0.2f, 0.0f);
                        break;

                    case "Tactile":
                    case "Silent Tactile":
                    case "Clicky":
                        if (keyDistances[i] < keys[i].GetKeySwitch().GetActuation())
                            keyDistances[i] = Math.Max(keyDistances[i] - 0.1f, 0.0f);

                        else
                            keyDistances[i] = Math.Max(keyDistances[i] - 0.3f, 0.0f);

                        break;

                    default:
                        Debug.LogFormat("<Mechanical Switches #{0}> Invalid category: {1}", moduleId, keys[i].GetKeySwitch().GetCategory());
                        ErrorFound();
                        keyDistances[i] = Math.Max(keyDistances[i] - 0.2f, 0.0f);
                        break;
                }

                if (!moduleSolved && keyRegistered[i] && keyDistances[i] <= keys[i].GetKeySwitch().GetActuation()) {
                    keyRegistered[i] = false;
                    RegisterRelease(i);
                }

                if (keyReachedEnd[i] > 0 && keyDistances[i] == 0.0f) {
                    keyReachedEnd[i] = 0;

                    switch (keys[i].GetKeySwitch().GetCategory()) {
                        case "Silent Linear":
                        case "Silent Tactile":
                            Audio.PlaySoundAtTransform("MS_SilentLinearRelease", KeySelectables[i].transform);
                            break;

                        case "Linear":
                        case "Tactile":
                            Audio.PlaySoundAtTransform("MS_LinearRelease", KeySelectables[i].transform);
                            break;

                        case "Clicky":
                            Audio.PlaySoundAtTransform("MS_ClickyRelease", KeySelectables[i].transform);
                            break;
                    }
                }

                else if (keyDistances[i] != 0.0f)
                    keyReachedEnd[i] = 1;
            }

            KeyTransforms[i].localPosition = new Vector3(KEY_X_POS[i], KEY_Y_POS - keyDistances[i] * DISTANCE_FACTOR, KEY_Z_POS);
        }
    }


    // Registers a key being pressed
    private IEnumerator RegisterHold(int no) {
        keyPressTimes[no] = Time.time;
        keyPressCount[no]++;
        var currentPressCount = keyPressCount[no];

        // Faulty keys
        if (no == faultyKey) {
            switch (currentFault) {
                case 1: // Key's light match color of its switch
                    Debug.LogFormat("<Mechanical Switches #{0}> Key {1} was avoided for {2} seconds.", moduleId, KEY_LABELS[no], keyPressTimes[no] - faultTime);

                    if (keyPressTimes[no] - faultTime < 60.0f) {
                        Debug.LogFormat("[Mechanical Switches #{0}] Key {1} was pressed before one minute elapsed. The module will strike upon release.", moduleId, KEY_LABELS[no]);
                        strikeReason = "The key was pressed before one minute elapsed.";
                        willStrike = true;
                    }

                    else
                        Debug.LogFormat("[Mechanical Switches #{0}] Key {1}'s fault was fixed.", moduleId, KEY_LABELS[no]);

                    currentFault = 0;
                    faultyKey = -1;
                    break;

                case 2: // Key's light doesn't match color of its switch
                case 3: // Morse Code letter that's not on a lit indicator and its value is not equal to the last digit in the serial number
                    if (holdCondition[no] != ((int) Bomb.GetTime()) % 10) {
                        Debug.LogFormat("[Mechanical Switches #{0}] You pressed key {1} when the last digit of the bomb's timer is {2}. The module will strike upon release.",
                            moduleId, KEY_LABELS[no], ((int) Bomb.GetTime()) % 10);

                        strikeReason = "The key was pressed at the wrong time.";
                        willStrike = true;
                    }

                    else
                        Debug.LogFormat("[Mechanical Switches #{0}] Key {1}'s fault was fixed.", moduleId, KEY_LABELS[no]);

                    currentFault = 0;
                    faultyKey = -1;
                    holdCondition[no] = -1;
                    break;

                case 4: // Morse Code letter that's not on a lit indicator and its value is equal to the last digit in the serial number
                    if (holdCondition[no] != ((int) Bomb.GetTime()) % 60 / 10) {
                        Debug.LogFormat("[Mechanical Switches #{0}] You pressed key {1} when the second-to-last digit of the bomb's timer is {2}. The module will strike upon release.",
                            moduleId, KEY_LABELS[no], ((int) Bomb.GetTime()) % 60 / 10);

                        strikeReason = "The key was pressed at the wrong time.";
                        willStrike = true;
                    }

                    else
                        Debug.LogFormat("[Mechanical Switches #{0}] Key {1}'s fault was fixed.", moduleId, KEY_LABELS[no]);

                    currentFault = 0;
                    faultyKey = -1;
                    holdCondition[no] = -1;
                    break;

                case 5: // Morse Code letter that's on a lit indicator
                    rapidTapTimes.Add(Time.time);
                    var tapCount = rapidTapTimes.Count;

                    if (tapCount > 2 && rapidTapTimes[tapCount - 1] - rapidTapTimes[tapCount - 3] <= 2.0f) {
                        Debug.LogFormat("[Mechanical Switches #{0}] Key {1}'s fault was fixed.", moduleId, KEY_LABELS[no]);

                        currentFault = 0;
                        faultyKey = -1;
                        break;
                    }

                    break;
            }
        }

        yield return new WaitForSeconds(1.0f); // Considers it a hold

        if (currentPressCount == keyPressCount[no] && keyRegistered[no]) {
            Debug.LogFormat("[Mechanical Switches #{0}] Holding key {1}.", moduleId, KEY_LABELS[no]);
            releaseCondition[no] = -1;

            var rand = UnityEngine.Random.Range(0, 9);
            var flickering = false;

            // We have to change the color of these lights to avoid dark or clear colored lights
            if (keys[no].GetKeySwitch().GetColorName() == "Black" || keys[no].GetKeySwitch().GetColorName() == "Clear" || keys[no].GetKeySwitch().GetColorName() == "Navy")
                rand = 8;

            if (no == faultyKey && currentFault == 5) {
                Debug.LogFormat("[Mechanical Switches #{0}] Key {1} was held when its fault was still not fixed. The module will strike upon release.", moduleId, KEY_LABELS[no]);
                strikeReason = "The key's fault was not fixed.";
                willStrike = true;

                currentFault = 0;
                faultyKey = -1;
                holdCondition[no] = -1;
            }

            else if (no != holdOrder[keysComplete]) {
                Debug.LogFormat("[Mechanical Switches #{0}] Key {1} was held out of order. The module will strike upon release.", moduleId, KEY_LABELS[no]);
                strikeReason = "The key was held in the wrong order.";
                willStrike = true;
            }

            switch (rand) {
                case 8: // Light color doesn't match switch color
                    var possibleColors = Enumerable.Range(0, RELEASE_COLORS.Length).ToArray();
                    var affectsRelease = false;

                    switch (keys[no].GetKeySwitch().GetColorName()) {
                        case "Red":
                            possibleColors = possibleColors.Where(x => x != 0).ToArray();
                            affectsRelease = true;
                            break;

                        case "Orange":
                        case "Burnt Orange":
                            possibleColors = possibleColors.Where(x => x != 1).ToArray();
                            affectsRelease = true;
                            break;

                        case "Yellow":
                        case "Dark Yellow":
                            possibleColors = possibleColors.Where(x => x != 2).ToArray();
                            affectsRelease = true;
                            break;

                        case "Green":
                            possibleColors = possibleColors.Where(x => x != 3).ToArray();
                            affectsRelease = true;
                            break;
                    }

                    var randColor = possibleColors[UnityEngine.Random.Range(0, possibleColors.Length)];
                    SetKeyLight(no, switches[RELEASE_COLORS[randColor]].GetId());
                    Debug.LogFormat("[Mechanical Switches #{0}] The key's light is {1}.", moduleId, switches[RELEASE_COLORS[randColor]].GetColorName());

                    if (affectsRelease) {
                        var x = keys[no].GetKeySwitch().GetColumn();
                        var y = RELEASE_COLOR_VALUES[randColor];

                        releaseCondition[no] = Math.Abs(x - y);
                        Debug.LogFormat("[Mechanical Switches #{0}] The key must be released when the last digit of the bomb's timer is {1}.", moduleId, releaseCondition[no]);
                    }

                    break;

                case 7: // Light color doesn't turn on
                    SetKeyLight(no, 34);
                    Debug.LogFormat("[Mechanical Switches #{0}] The key's light did not turn on (or turned off).", moduleId);

                    releaseCondition[no] = keys[no].GetKeySwitch().GetColumn();
                    Debug.LogFormat("[Mechanical Switches #{0}] The key must be released when the last digit of the bomb's timer is {1}.", moduleId, releaseCondition[no]);
                    break;

                case 6: // Light is rapidly flickering
                    SetKeyLight(no, keys[no].GetKeySwitch().GetId());
                    releaseCondition[no] = -2;
                    flickering = true;

                    Debug.LogFormat("[Mechanical Switches #{0}] The key's light is rapidly flickering. Release it immediately.", moduleId);
                    break;

                default:
                    SetKeyLight(no, keys[no].GetKeySwitch().GetId());
                    Debug.LogFormat("[Mechanical Switches #{0}] The key's light is {1}.", moduleId, keys[no].GetKeySwitch().GetColorName());
                    break;
            }

            if (flickering) {
                var showColor = false;

                while (keyRegistered[no]) {
                    showColor = !showColor;

                    if (showColor)
                        SetKeyLight(no, keys[no].GetKeySwitch().GetId());

                    else
                        SetKeyLight(no, 34);

                    yield return new WaitForSeconds(0.1f);
                }
            }
        }
    }

    // Registers a key being released
    private void RegisterRelease(int no) {
        var elapsed = Time.time - keyPressTimes[no];
        Debug.LogFormat("<Mechanical Switches #{0}> Key {1} was pressed for {2} seconds.", moduleId, KEY_LABELS[no], elapsed);

        SetKeyLight(no, 34);

        if (willStrike) { // Holding condition was wrong
            Debug.LogFormat("[Mechanical Switches #{0}] {1} Strike!", moduleId, strikeReason);
            StartCoroutine(ModuleStrike());
        }

        else if (releaseCondition[no] == -2 && elapsed >= 3.0f) { // Key was not released fast enough
            Debug.LogFormat("[Mechanical Switches #{0}] The key was not released quick enough. Strike!", moduleId);
            StartCoroutine(ModuleStrike());
        }

        else if (releaseCondition[no] != -2 && elapsed < 5.0f && elapsed >= 1.0f) { // Key was not held for long enough
            Debug.LogFormat("[Mechanical Switches #{0}] The key was not held for at least five seconds. Strike!", moduleId);
            StartCoroutine(ModuleStrike());
        }

        else if (releaseCondition[no] >= 0 && ((int) Bomb.GetTime()) % 10 != releaseCondition[no]) { // Key was released at the wrong time
            Debug.LogFormat("[Mechanical Switches #{0}] You released the key when the last digit was {1}. Strike!", moduleId, ((int) Bomb.GetTime()) % 10);
            StartCoroutine(ModuleStrike());
        }

        else if (elapsed >= 1.0f) { // Successful hold
            keysComplete++;
            Debug.LogFormat("[Mechanical Switches #{0}] The key was held and released successfully. You have held {1} key(s).", moduleId, keysComplete);

            willFault[no] = false;

            if (keysComplete == 5) {
                Debug.LogFormat("[Mechanical Switches #{0}] All five keys were held successfully. Module solved!", moduleId);
                ModuleSolve();
            }
        }

        else if (willFault[no]) { // Reveals the faulty key
            willFault[no] = false;
            StartCoroutine(RevealFault(no));
        }

        releaseCondition[no] = -1;
    }

    // Reveals the fault of one of the keys
    private IEnumerator RevealFault(int no) {
        faultTime = Time.time;
        faultyKey = no;
        Debug.LogFormat("[Mechanical Switches #{0}] Key {1} is faulty.", moduleId, KEY_LABELS[no]);

        // We have to change the color of these lights to avoid dark or clear colored lights
        if (keys[no].GetKeySwitch().GetColorName() == "Black" || keys[no].GetKeySwitch().GetColorName() == "Clear" || keys[no].GetKeySwitch().GetColorName() == "Navy")
            currentFault = UnityEngine.Random.Range(2, 4);

        else
            currentFault = UnityEngine.Random.Range(1, 4);


        switch (currentFault) {
            case 1: // Key's light match color of its switch
                SetKeyLight(no, keys[no].GetKeySwitch().GetId());
                Debug.LogFormat("[Mechanical Switches #{0}] The key's light matches the color of its switch. Do not interact with the key for at least one minute.", moduleId);
                break;

            case 2: // Key's light doesn't match color of its switch
                var possibleColors = Enumerable.Range(0, HOLD_COLORS.Length).ToArray();

                switch (keys[no].GetKeySwitch().GetColorName()) {
                    case "Blue":
                    case "Navy":
                    case "Pale Blue":
                        possibleColors = possibleColors.Where(x => x != 0).ToArray();
                        break;

                    case "Clear":
                    case "White":
                        possibleColors = possibleColors.Where(x => x != 1).ToArray();
                        break;

                    case "Aliaz":
                    case "Brown":
                    case "Burgundy":
                    case "Copper":
                    case "Red":
                        possibleColors = possibleColors.Where(x => x != 2).ToArray();
                        break;

                    case "Green":
                    case "Jade":
                    case "Tealios":
                        possibleColors = possibleColors.Where(x => x != 3).ToArray();
                        break;

                    case "Burnt Orange":
                    case "Orange":
                        possibleColors = possibleColors.Where(x => x != 4).ToArray();
                        break;

                    case "Purple":
                    case "Zealios":
                        possibleColors = possibleColors.Where(x => x != 5).ToArray();
                        break;

                    case "Bronze":
                    case "Dark Yellow":
                    case "Gold":
                    case "Yellow":
                        possibleColors = possibleColors.Where(x => x != 6).ToArray();
                        break;

                    case "Black":
                    case "Grey":
                    case "Silver":
                        possibleColors = possibleColors.Where(x => x != 7).ToArray();
                        break;
                }

                var randColor = possibleColors[UnityEngine.Random.Range(0, possibleColors.Length)];
                SetKeyLight(no, switches[HOLD_COLORS[randColor]].GetId());
                holdCondition[no] = HOLD_COLOR_VALUES[randColor];

                Debug.LogFormat("[Mechanical Switches #{0}] The key's light is {1}. You can only interact with the key when the last digit of the bomb's timer is {2}.",
                    moduleId, switches[HOLD_COLORS[randColor]].GetColorName(), holdCondition[no]);

                StartCoroutine(MinuteTimer(no));
                break;

            case 3: // Morse Code letter
                var randLetter = UnityEngine.Random.Range(0, 26);

                // Make sure the defuser would have adequate time to press the key within the minute
                if (LETTER_ROOTS[randLetter] == Bomb.GetSerialNumberNumbers().Last()) {
                    var desiredTenSecond = ((int) Bomb.GetTime()) % 60 / 10;

                    if (ZenModeActive) {
                        desiredTenSecond--;
                        desiredTenSecond += desiredTenSecond < 0 ? 6 : 0;
                    }

                    else {
                        desiredTenSecond++;
                        desiredTenSecond %= 6;
                    }

                    if (LETTER_ROOTS[randLetter] % 6 != desiredTenSecond) {
                        randLetter++;
                        randLetter %= 26;
                    }
                }

                Debug.LogFormat("[Mechanical Switches #{0}] The key's light is transmitting {1} in Morse Code.", moduleId, LETTER_NAMES[randLetter]);

                if (Bomb.GetOnIndicators().Any(x => x.Contains(LETTER_NAMES[randLetter]))) {
                    currentFault = 5;
                    Debug.LogFormat("[Mechanical Switches #{0}] You must tap the key three times within two seconds.", moduleId);
                }

                else if (LETTER_ROOTS[randLetter] == Bomb.GetSerialNumberNumbers().Last()) {
                    currentFault = 4;
                    holdCondition[no] = LETTER_ROOTS[randLetter] % 6;
                    Debug.LogFormat("[Mechanical Switches #{0}] You can only interact with the key when the second-to-last digit of the bomb's timer is {1}.", moduleId, holdCondition[no]);
                }

                else {
                    holdCondition[no] = LETTER_ROOTS[randLetter];
                    Debug.LogFormat("[Mechanical Switches #{0}] You can only interact with the key when the last digit of the bomb's timer is {1}.", moduleId, holdCondition[no]);
                }

                StartCoroutine(MinuteTimer(no));

                // Transmits the morse
                var morseIndex = 0;
                var transmitOn = true;
                while (faultyKey == no) {
                    if (transmitOn) {
                        SetKeyLight(no, 35);

                        if (LETTER_MORSE[randLetter][morseIndex] == '-')
                            yield return new WaitForSeconds(0.6f);

                        else
                            yield return new WaitForSeconds(0.2f);
                    }

                    else {
                        SetKeyLight(no, 34);
                        morseIndex++;

                        if (morseIndex == LETTER_MORSE[randLetter].Length) {
                            morseIndex = 0;
                            yield return new WaitForSeconds(1.0f);
                        }

                        else
                            yield return new WaitForSeconds(0.2f);
                    }

                    transmitOn = !transmitOn;
                }

                break;
        }

        yield return null;
    }

    // Sets a timer for fixing the fault
    private IEnumerator MinuteTimer(int no) {
        var halfSeconds = 0;

        while (faultyKey == no) {
            if (halfSeconds == 120) {
                Debug.LogFormat("[Mechanical Switches #{0}] The faulty key was not fixed after one minute. Strike!", moduleId);
                GetComponent<KMBombModule>().HandleStrike();
                break;
            }

            yield return new WaitForSeconds(0.5f);
            halfSeconds++;
        }
    }


    // Changes the light on the key
    private void SetKeyLight(int no, int color) {
        switch (color) {
            case 34: // Black
                KeyPointLights[no].color = new Color(0.0f, 0.0f, 0.0f);
                break;

            case 35: // White
                KeyPointLights[no].color = new Color(1.0f, 1.0f, 1.0f);
                break;

            default: // Color of a switch
                KeyPointLights[no].color = SwitchColors[color];
                break;
        }

        var xOff = color % 6;
        var yOff = 5 - color / 6;

        KeyLights[no].material.SetTextureOffset("_MainTex", new Vector2(((float) xOff) / 6.0f, ((float) yOff) / 6.0f));

        if (colorblind)
            KeyColorblindTexts[no].text = GetColorblindText(color);
    }

    // Gets the colorblind text for the keys and switches
    private string GetColorblindText(int color) {
        if (color == 34) // Black
            return "Bk";

        else if (color == 35) // White
            return "Wh";

        switch (switches[color].GetColorName()) {
            case "Aliaz": return "Al";
            case "Black": return "Bk";
            case "Blue": return "Bl";
            case "Bronze": return "Bz";
            case "Brown": return "Bn";
            case "Burgundy": return "Bu";
            case "Burnt Orange": return "BO";
            case "Clear": return "Cl";
            case "Copper": return "Co";
            case "Dark Yellow": return "DY";
            case "Gold": return "Go";
            case "Green": return "Gr";
            case "Grey": return "Gy";
            case "Jade": return "Ja";
            case "Navy": return "Na";
            case "Orange": return "Or";
            case "Pale Blue": return "PB";
            case "Purple": return "Pu";
            case "Red": return "Re";
            case "Silver": return "Si";
            case "Tealios": return "Te";
            case "Yellow": return "Ye";
            case "White": return "Wh";
            case "Zealios": return "Ze";

            default:
                Debug.LogFormat("<Mechanical Switches #{0}> Invalid color name: {1}", moduleId, color);
                ErrorFound();
                return "";
        }
    }


    // Module solves
    private void ModuleSolve() {
        moduleSolved = true;
        GetComponent<KMBombModule>().HandlePass();
    }

    // Module strikes
    private IEnumerator ModuleStrike() {
        GetComponent<KMBombModule>().HandleStrike();
        canPress = false;
        willStrike = false;

        yield return new WaitForSeconds(1.0f);

        Debug.LogFormat("[Mechanical Switches #{0}] Resetting module...", moduleId);
        StartCoroutine(ResetModule());
    }

    // The module threw an error somewhere
    private void ErrorFound() {
        Debug.LogFormat("[Mechanical Switches #{0}] Something went wrong. Press any key to solve the module. Please contact Espik and provide the logfile.", moduleId);
        canAutoSolve = true;
    }


#pragma warning disable 414
    private bool ZenModeActive;
#pragma warning restore 414
}