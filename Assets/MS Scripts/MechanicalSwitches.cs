using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KModkit;

/* Things left to implement:
 * 
 * Find and fix a bug in the release time
 * Ability to display a Morse Code letter on a key's lights
 * Test if a Morse Code letter is found in any of the bombs lit indicators
 * Test if a key has been tapped three times in the last two seconds
 * Make a clear material
 * Make clear keys have a clear material
 * Animate the key pressing
 * Add a colorblind mode
 * Extend the faulty key release time if Twitch Plays is active
 * Change the lights on the keys to light LEDs on the screens
 * Fix an issue where the timer doesn't start and the first iteration is not logged
 * Implement TP code
 */

// Repo module desc: "Select each button and figure out which keys make which sounds. Using these sounds, figure out when to hold each button down and when you should release them. Watch out for faulty keys!"

public class MechanicalSwitches : MonoBehaviour {
    public KMAudio Audio;
    public KMBombInfo Bomb;
    public KMBombModule Module;

    public KMSelectable[] Keys;
    public Renderer[] Screens;
    public Material[] Colors;
    public Material[] WrongColors;
    public Material BlankColor;
    public Light[] Lights;
    public TextMesh BigScreenText;

    // Logging info
    private static int moduleIdCounter = 1;
    private int moduleId;
    private bool moduleSolved = false;

    // General solving info
    private MechanicalKey[] mechanicalKeys = new MechanicalKey[5];
    private KeySwitch[] switches = new KeySwitch[39];
    private int[] keyOrder = new int[5];
    private bool loopBreak = false;

    // Button press info
    private int keyInOrder = 0;
    private int keyHolding = -1;
    private int keyPresses = 0;
    private int releaseTime = -1;
    private float faultyReleaseTime = 2.5f;
    private float holdingTime = -0.1f;
    private bool willStrike = false;

    // Morse letters
    private char[] letters = { 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z' };
    private int[] letterDigRoot = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 1, 2, 3, 4, 5, 6, 7, 8, 9, 1, 2, 3, 4, 5, 6, 7, 8 };
    private string[] letterValues = { "10111000", "111010101000" , "11101011101000", "1110101000", "1000", "101011101000", "111011101000", "1010101000", "101000", "1011101110111000", "111010111000", "111010111000", "1110111000",
                                       "11101000", "11101110111000", "10111011101000", "1110111010111000", "1011101000", "10101000", "111000", "1010111000", "101010111000", "101110111000", "11101010111000", "1110101110111000", "11101110101000"};


    // Table grid info
    private int[] holdOrder = new int[5];
    private int[][] tableGrid = new int[9][];
    private int[][] tableGridPos = new int[9][];
    private char[] serialNumber = new char[6];
    private bool[] serialNumberConversions = new bool[15];
    private bool[] rules = new bool[41];
    private int iterations = 0;

    // Ran as bomb loads
    private void Awake() {
        moduleId = moduleIdCounter++;

        // Delegation
        for (int i = 0; i < Keys.Length; i++) {
            int j = i;

            Keys[i].OnInteract += delegate () {
                KeyPressed(j);
                return false;
            };

            Keys[i].OnInteractEnded += delegate () {
                KeyReleased(j);
            };
        }
    }

    // Gets module ready
    private void Start() {
        SetRules();
        SetSwitches();
        TurnOffLights();

        // Turns serial number into movement directions
        serialNumber = Bomb.GetSerialNumber().ToCharArray();
        ConvertSerialNumber();

        Module.OnActivate += OnActivate;
    }

    // Runs when lights turn on
    private void OnActivate() {
        BigScreenText.text = "";
        ModuleReset();
    }


    // If module strikes
    private void Strike() {
        willStrike = false;
        Debug.LogFormat("[Mechanical Switches #{0}] Strike! Module reseting...\n\n", moduleId);
        GetComponent<KMBombModule>().HandleStrike();
        ModuleReset();
    }

    // If module solves
    private void Solve() {
        Debug.LogFormat("[Mechanical Switches #{0}] Module Solved!", moduleId);
        GetComponent<KMBombModule>().HandlePass();
        moduleSolved = true;
        keyPresses = 0;
    }

    // Module resets and/or starts
    private void ModuleReset() {
        iterations = 0;
        SetKeys();
        keyOrder = GetOrderRule();
        GetHoldOrder();
        //holdOrder = keyOrder; // Placeholder when GetHoldOrder() is turned off
        keyInOrder = 0;
        TurnOffLights();
    }

    // Turns off the lights
    private void TurnOffLights() {
        for (int i = 0; i < Lights.Length; i++) {
            Lights[i].enabled = false;
        }
    }


    // Sets the switches in order
    private void SetSwitches() {
        /* Colors:
         * 
         * 0:   Aliaz
         * 1:   Black_Cherry
         * 2:   Black_Generic
         * 3:   Blue
         * 4:   Bronze
         * 5:   Brown
         * 6:   Burgundy
         * 7:   Burnt Orange
         * 8:   Clear_Cherry
         * 9:   Clear_Gateron
         * 10:  Copper
         * 11:  Dark Yellow
         * 12:  Gold
         * 13:  Green_Generic
         * 14:  Green_Kailh
         * 15:  Green_Razer
         * 16:  Grey
         * 17:  Jade
         * 18:  Navy
         * 19:  Orange
         * 20:  Pale Blue
         * 21:  Purple
         * 22:  Red
         * 23:  Silver
         * 24:  Tealios
         * 25:  White
         * 26:  Yellow
         * 27:  Zealios
         */

        int zealiosRandom = UnityEngine.Random.Range(62, 78);
        int aliazRandom = UnityEngine.Random.Range(60, 100);

        switches[0] = new KeySwitch("Cherry Black", "Cherry", "Black", Colors[1], false, "Linear", 60.0f, 2.0f, 4.0f, 1); // Cherry Black
        switches[1] = new KeySwitch("Cherry Speed Silver", "Cherry", "Speed Silver", Colors[23], false, "Linear", 45.0f, 1.2f, 3.4f, 1); // Cherry Speed Silver
        switches[2] = new KeySwitch("Gateron Black", "Gateron", "Black", Colors[2], false, "Linear", 50.0f, 2.0f, 4.0f, 1); // Gateron Black
        switches[3] = new KeySwitch("Kailh Box Black", "Kailh Box", "Black", Colors[2], false, "Linear", 60.0f, 1.8f, 3.6f, 1); // Kailh Box Black
        switches[4] = new KeySwitch("Kailh Pro Burgundy", "Kailh Pro", "Burgundy", Colors[6], false, "Linear", 50.0f, 1.7f, 3.6f, 1); // Kailh Pro Burgundy
        switches[5] = new KeySwitch("Cherry Red", "Cherry", "Red", Colors[22], false, "Linear", 45.0f, 2.0f, 4.0f, 2); // Cherry Red
        switches[6] = new KeySwitch("Gateron Red", "Gateron", "Red", Colors[22], false, "Linear", 50.0f, 2.0f, 4.0f, 2); // Gateron Red
        switches[7] = new KeySwitch("Kailh Box Red", "Kailh Box", "Red", Colors[22], false, "Linear", 45.0f, 1.8f, 3.6f, 2); // Kailh Box Red
        switches[8] = new KeySwitch("Kailh Pro Purple", "Kailh Pro", "Purple", Colors[21], false, "Tactile", 50.0f, 1.7f, 3.6f, 2); // Kailh Pro Purple
        switches[9] = new KeySwitch("Cherry Brown", "Cherry", "Brown", Colors[5], false, "Tactile", 45.0f, 2.0f, 4.0f, 3); // Cherry Brown
        switches[10] = new KeySwitch("Gateron Brown", "Gateron", "Brown", Colors[5], false, "Tactile", 50.0f, 2.0f, 4.0f, 3); // Gateron Brown
        switches[11] = new KeySwitch("Kailh Box Brown", "Kailh Box", "Brown", Colors[5], false, "Tactile", 60.0f, 1.8f, 3.6f, 3); // Kailh Box Brown
        switches[12] = new KeySwitch("Kalih Pro Green", "Kailh Pro", "Green", Colors[14], false, "Clicky", 50.0f, 1.7f, 3.6f, 3); // Kailh Pro Green
        switches[13] = new KeySwitch("Cherry Blue", "Cherry", "Blue", Colors[3], false, "Clicky", 50.0f, 2.2f, 4.0f, 4); // Cherry Blue
        switches[14] = new KeySwitch("Gateron Blue", "Gateron", "Blue", Colors[3], false, "Clicky", 55.0f, 2.2f, 4.0f, 4); // Gateron Blue
        switches[15] = new KeySwitch("Kailh Box White", "Kailh Box", "White", Colors[25], false, "Clicky", 55.0f, 1.8f, 3.6f, 4); // Kailh Box White
        switches[16] = new KeySwitch("Kailh Speed Silver", "Kailh", "Speed Silver", Colors[23], false, "Linear", 50.0f, 1.1f, 3.5f, 4); // Kailh Speed Silver
        switches[17] = new KeySwitch("Cherry Clear", "Cherry", "Clear", Colors[8], true, "Tactile", 65.0f, 2.0f, 4.0f, 5); // Cherry Clear
        switches[18] = new KeySwitch("Gateron Clear", "Gateron", "Clear", Colors[9], true, "Linear", 35.0f, 2.0f, 4.0f, 5); // Gateron Clear
        switches[19] = new KeySwitch("Kailh Box Navy", "Kailh Box", "Navy", Colors[18], false, "Clicky", 75.0f, 1.7f, 3.6f, 5); // Kailh Box Navy
        switches[20] = new KeySwitch("Kailh Speed Copper", "Kailh", "Copper", Colors[10], false, "Tactile", 50.0f, 1.1f, 3.5f, 5); // Kailh Speed Copper
        switches[21] = new KeySwitch("Cherry White", "Cherry", "White", Colors[25], false, "Clicky", 85.0f, 2.0f, 4.0f, 6); // Cherry White
        switches[22] = new KeySwitch("Gateron Yellow", "Gateron", "Yellow", Colors[26], false, "Linear", 50.0f, 2.0f, 4.0f, 6); // Gateron Yellow
        switches[23] = new KeySwitch("Kailh Box Jade", "Kailh Box", "Jade", Colors[17], false, "Clicky", 65.0f, 1.7f, 3.6f, 6); // Kailh Box Jade
        switches[24] = new KeySwitch("Kailh Speed Bronze", "Kailh", "Bronze", Colors[4], false, "Clicky", 50.0f, 1.1f, 3.5f, 6); // Kailh Speed Bronze
        switches[25] = new KeySwitch("Cherry Green", "Cherry", "Green", Colors[13], false, "Tactile", 80.0f, 2.2f, 4.0f, 7); // Cherry Green
        switches[26] = new KeySwitch("Gateron Green", "Gateron", "Green", Colors[13], false, "Clicky", 80.0f, 2.0f, 4.0f, 7); // Gateron Green
        switches[27] = new KeySwitch("Kailh Box Dark Yellow", "Kailh Box", "Dark Yellow", Colors[11], false, "Linear", 70.0f, 1.8f, 3.6f, 7); // Kailh Box Dark Yellow
        switches[28] = new KeySwitch("Kailh Speed Gold", "Kailh", "Speed Gold", Colors[12], false, "Clicky", 50.0f, 1.4f, 3.5f, 7); // Kailh Speed Gold
        switches[29] = new KeySwitch("Razer Green", "Razer", "Green", Colors[15], false, "Clicky", 50.0f, 1.9f, 4.0f, 7); // Razer Green
        switches[30] = new KeySwitch("Cherry Grey (Linear)", "Cherry", "Grey", Colors[16], false, "Linear", 80.0f, 2.0f, 4.0f, 8); // Cherry Grey (Linear)
        switches[31] = new KeySwitch("Gateron Tealios", "Gateron", "Tealios", Colors[24], false, "Linear", 67.0f, 2.0f, 4.0f, 8); // Gateron Tealios
        switches[32] = new KeySwitch("Kailh Box Burnt Orange", "Kailh Box", "Burnt Orange", Colors[7], false, "Tactile", 70.0f, 1.8f, 3.6f, 8); // Kailh Box Burnt Orange
        switches[33] = new KeySwitch("Razer Orange", "Razer", "Orange", Colors[19], false, "Silent", 45.0f, 1.9f, 4.0f, 8); // Razer Orange
        switches[34] = new KeySwitch("Cherry Grey (Tactile)", "Cherry", "Grey", Colors[16], false, "Tactile", 80.0f, 2.0f, 4.0f, 9); // Cherry Grey (Tactile)
        switches[35] = new KeySwitch("Gateron Zealios", "Gateron", "Zealios", Colors[27], false, "Tactile", (float) zealiosRandom, 2.0f, 4.0f, 9); // Gateron Zealios
        switches[36] = new KeySwitch("Gateron Aliaz", "Gateron", "Aliaz", Colors[0], false, "Silent", (float) aliazRandom, 2.0f, 4.0f, 9); // Gateron Aliaz
        switches[37] = new KeySwitch("Kailh Box Pale Blue", "Kailh Box", "Pale Blue", Colors[20], false, "Clicky", 70.0f, 1.8f, 3.6f, 9); // Kailh Box Pale Blue
        switches[38] = new KeySwitch("Razer Yellow", "Razer", "Yellow", Colors[26], false, "Linear", 45.0f, 1.2f, 3.5f, 9); // Razer Yellow
    }

    
    // Key pressed
    private void KeyPressed(int i) {
        Keys[i].AddInteractionPunch(mechanicalKeys[i].GetKeySwitch().GetForce() / 100.0f);
        PlayKeySound(mechanicalKeys[i].GetKeySwitch().GetSound());
        Lights[i].enabled = true;

        keyHolding = i;
        holdingTime = Time.time;

        // Displays switch information on the screen
        BigScreenText.text = "FOR " + mechanicalKeys[i].GetKeySwitch().GetForce().ToString() +
            " CN\nACT " + mechanicalKeys[i].GetKeySwitch().GetActuation().ToString() +
            " MM\nDIS " + mechanicalKeys[i].GetKeySwitch().GetTravelDistance().ToString() +
            " MM";

        // Keeps track of the number of key presses throughout the module
        if (moduleSolved == false)
            keyPresses++;

        // If the key is not safe to press
        if (mechanicalKeys[i].GetPressSafe() == false ||
            (mechanicalKeys[i].GetPressTime() != -1 && Bomb.GetTime() % 10 != mechanicalKeys[i].GetPressTime() && mechanicalKeys[i].GetDoublePressTime() == false) ||
            mechanicalKeys[i].GetPressTime() != -1 && Bomb.GetTime() % 10 != mechanicalKeys[i].GetPressTime() && mechanicalKeys[i].GetDoublePressTime() == true && (int) Math.Round(Bomb.GetTime() % 60 / 10) == mechanicalKeys[i].GetPressTime() % 6) {

            willStrike = true;
        }

        // Applies conditions if present when holding keys
        if (mechanicalKeys[i].GetCondition() > 0 && mechanicalKeys[i].GetCondition() < 4)
            StartCoroutine(ApplyHoldConditions(keyPresses, i, mechanicalKeys[i].GetCondition()));

        // Adds one to the rapid tapping
        if (mechanicalKeys[i].GetNeedRapidTapping() == true) {
            // 
            mechanicalKeys[i].SetNeedRapidTapping(false); // Placeholder
        }

        // Plays holding animation
    }

    // Key released
    private void KeyReleased(int i) {
        PlayReleaseSound(mechanicalKeys[i].GetKeySwitch().GetSound());
        Lights[i].enabled = false;


        // Key was tapped or module is solved
        if ((Time.time - holdingTime < 0.5f && willStrike == false)
            || moduleSolved == true) {
            // Applies conditions if present when tapping keys
            if (mechanicalKeys[i].GetCondition() > 3)
                ApplyTapConditions(i, mechanicalKeys[i].GetCondition());

            // If the key still needs to be rapidly tapped
            if (mechanicalKeys[i].GetNeedRapidTapping() == true)
                Lights[i].enabled = true;
        }

        // Incorrect answer criteria
        else if (mechanicalKeys[i].GetNumber() != holdOrder[keyInOrder] ||
            (releaseTime != -1 && Bomb.GetTime() % 10 != releaseTime) || 
            (mechanicalKeys[i].GetCondition() != 2 && Time.time - holdingTime < 5.0f) ||
            (mechanicalKeys[i].GetCondition() == 2 && Time.time - holdingTime > faultyReleaseTime) ||
            mechanicalKeys[i].GetNeedRapidTapping() == true) {

            willStrike = true;
        }

        // Advances through the module
        else {
            Debug.LogFormat("[Mechanical Switches #{0}] Key no. {1} was held correctly.", moduleId, mechanicalKeys[i].GetNumber());
            keyInOrder++;
        }


        // Reset info
        holdingTime = -0.1f;
        keyHolding = -1;
        releaseTime = -1;
        mechanicalKeys[i].SetPressTime(-1);
        mechanicalKeys[i].SetDoublePressTime(false);
        mechanicalKeys[i].SetNeedRapidTapping(false);

        BigScreenText.text = "";

        // Plays releasing animation

        // Striking
        if (willStrike == true) {
            Debug.LogFormat("[Mechanical Switches #{0}] Key no. {1} was not held correctly.", moduleId, mechanicalKeys[i].GetNumber());
            Strike();
        }

        if (keyInOrder > 4)
            Solve();
    }

    // Plays the key sound on hold
    private void PlayKeySound(string sound) {
        if (sound == "Clicky")
            Audio.PlaySoundAtTransform("MS_ClickyHold", transform);

        else if (sound == "Linear")
            Audio.PlaySoundAtTransform("MS_LinearHold", transform);

        else if (sound == "Tactile")
            Audio.PlaySoundAtTransform("MS_TactileHold", transform);
    }

    // Plays the key sound on release
    private void PlayReleaseSound(string sound) {
        if (sound == "Clicky")
            Audio.PlaySoundAtTransform("MS_ClickyRelease", transform);

        else if (sound == "Linear")
            Audio.PlaySoundAtTransform("MS_LinearRelease", transform);

        else if (sound == "Tactile")
            Audio.PlaySoundAtTransform("MS_TactileRelease", transform);
    }


    // Applies conditions when holding
    private IEnumerator ApplyHoldConditions(int presses, int i, int condition) {
        yield return new WaitForSeconds(0.5f);

        // If the same key is still in the same hold
        if (keyHolding == i && presses == keyPresses) {
            // Incorrect color
            if (condition == 1) {
                int selected = UnityEngine.Random.Range(0, WrongColors.Length - 1);

                /* 0: Red
                 * 1: Orange
                 * 2: Yellow
                 * 3: Green
                 * 4: Blue
                 * 5: Purple
                 * 6: White
                 */

                // Avoids conflicts with the same color
                if (selected == 0 && mechanicalKeys[i].GetKeySwitch().GetColor() == "Red")
                    condition = 0;

                else if (selected == 1 && mechanicalKeys[i].GetKeySwitch().GetColor() == "Orange")
                    condition = 0;

                else if (selected == 2 && mechanicalKeys[i].GetKeySwitch().GetColor() == "Yellow")
                    condition = 0;

                else if (selected == 3 && mechanicalKeys[i].GetKeySwitch().GetColor() == "Green")
                    condition = 0;

                else if (selected == 4 && mechanicalKeys[i].GetKeySwitch().GetColor() == "Blue")
                    condition = 0;

                else if (selected == 5 && mechanicalKeys[i].GetKeySwitch().GetColor() == "Purple")
                    condition = 0;

                else if (selected == 6 && mechanicalKeys[i].GetKeySwitch().GetColor() == "White")
                    condition = 0;


                if (condition == 1) {
                    mechanicalKeys[i].SetMaterial(WrongColors[selected]);
                    Debug.LogFormat("[Mechanical Switches #{0}] Key no. {1} is flashing an incorrect color.", moduleId, mechanicalKeys[i].GetNumber());

                    // If the release time is affected
                    if (mechanicalKeys[i].GetKeySwitch().GetColor() == "Red" ||
                        mechanicalKeys[i].GetKeySwitch().GetColor() == "Orange" ||
                        mechanicalKeys[i].GetKeySwitch().GetColor() == "Burnt Orange" ||
                        mechanicalKeys[i].GetKeySwitch().GetColor() == "Yellow" ||
                        mechanicalKeys[i].GetKeySwitch().GetColor() == "Dark Yellow" ||
                        mechanicalKeys[i].GetKeySwitch().GetColor() == "Green") {

                        int a = mechanicalKeys[i].GetKeySwitch().GetColumn();
                        int b = 0;

                        switch (selected) {
                            case 0: b = 1; break; // Red
                            case 1: b = 2; break; // Orange
                            case 2: b = 3; break; // Yellow
                            case 3: b = 4; break; // Green
                            case 4: b = 5; break; // Blue
                            case 5: b = 6; break; // Purple
                            case 6: b = 10; break; // White
                            default: b = 0; break;
                        }

                        if (a >= b)
                            releaseTime = a - b;

                        else
                            releaseTime = b - a;


                        Debug.LogFormat("[Mechanical Switches #{0}] Release key no. {1} when the last digit of the timer is a {2}.", moduleId, mechanicalKeys[i].GetNumber(), releaseTime);
                    }
                }

                mechanicalKeys[i].SetCondition(0);
            }

            // Faulty Key
            else if (condition == 2) {
                // if (Twitch Plays is on) {
                //      faultyReleaseTime = 10.5f;
                //      Display a message to Twitch chat "The key being held is faulty. It must be released in 10 seconds."
                // }

                Debug.LogFormat("[Mechanical Switches #{0}] Key no. {1} is faulty. Release it immediately.", moduleId, mechanicalKeys[i].GetNumber());
                StartCoroutine(FaultyKey(presses, i));
            }

            // No color flashing
            else if (condition == 3) {
                mechanicalKeys[i].SetMaterial(BlankColor);
                Debug.LogFormat("[Mechanical Switches #{0}] Key no. {1} is not flashing a color", moduleId, mechanicalKeys[i].GetNumber());

                if (mechanicalKeys[i].GetKeySwitch().GetColor() != "Clear") {
                    releaseTime = mechanicalKeys[i].GetKeySwitch().GetColumn();
                    Debug.LogFormat("[Mechanical Switches #{0}] Release key no. {1} when the last digit of the timer is a {2}.", moduleId, mechanicalKeys[i].GetNumber(), releaseTime);
                }

                mechanicalKeys[i].SetCondition(0);
            }
        }
    }

    // Faulty key
    private IEnumerator FaultyKey(int presses, int i) {
        yield return new WaitForSeconds(0.15f);

        Lights[i].enabled = false;

        yield return new WaitForSeconds(0.15f);

        if (keyHolding == i && presses == keyPresses) {
            Lights[i].enabled = true;
            StartCoroutine(FaultyKey(presses, i));
        }
    }

    // Applies conditions after tapping
    private void ApplyTapConditions(int i, int condition) {
        // Light always lit and correct color
        if (condition == 4) {
            Lights[i].enabled = true;
            mechanicalKeys[i].SetPressSafe(false);
            Debug.LogFormat("[Mechanical Switches #{0}] Key no. {1}'s light is still on after tapping, and the color is the same. Do not interact with the key for at least a minute.", moduleId, mechanicalKeys[i].GetNumber());
            mechanicalKeys[i].SetCondition(0);
            StartCoroutine(WaitMinute(i));
        }

        // Light always lit and incorrect color
        else if (condition == 5) {
            // Incorrect color
            int selected = UnityEngine.Random.Range(0, WrongColors.Length);

            /* 0: Red
             * 1: Orange
             * 2: Yellow
             * 3: Green
             * 4: Blue
             * 5: Purple
             * 6: White
             * 7: Black
             */

            // Avoids conflicts with the same color
            if (selected == 0 && mechanicalKeys[i].GetKeySwitch().GetColor() == "Red")
                condition = 0;

            else if (selected == 1 && mechanicalKeys[i].GetKeySwitch().GetColor() == "Orange")
                condition = 0;

            else if (selected == 2 && mechanicalKeys[i].GetKeySwitch().GetColor() == "Yellow")
                condition = 0;

            else if (selected == 3 && mechanicalKeys[i].GetKeySwitch().GetColor() == "Green")
                condition = 0;

            else if (selected == 4 && mechanicalKeys[i].GetKeySwitch().GetColor() == "Blue")
                condition = 0;

            else if (selected == 5 && mechanicalKeys[i].GetKeySwitch().GetColor() == "Purple")
                condition = 0;

            else if (selected == 6 && mechanicalKeys[i].GetKeySwitch().GetColor() == "White")
                condition = 0;

            else if (selected == 7 && mechanicalKeys[i].GetKeySwitch().GetColor() == "Black")
                condition = 0;


            if (condition == 5) {
                Lights[i].enabled = true;

                int n = 0;

                switch (selected) {
                    case 0: n = 2; break; // Red
                    case 1: n = 4; break; // Orange
                    case 2: n = 7; break; // Yellow
                    case 3: n = 2; break; // Green
                    case 4: n = 0; break; // Blue
                    case 5: n = 6; break; // Purple
                    case 6: n = 0; break; // White
                    case 7: n = 9; break; // Black
                    default: n = 0; break;
                }

                mechanicalKeys[i].SetPressTime(n);
                Debug.LogFormat("[Mechanical Switches #{0}] Key no. {1}'s light is still on after tapping, and the color is the not the same. Only interact with the key when the last digit of the timer is a {2}.", moduleId, mechanicalKeys[i].GetNumber(), n);
                mechanicalKeys[i].SetCondition(0);
            }
        }

        // Contantly lit in Morse Code
        else if (condition == 6) {
            int randomNo = UnityEngine.Random.Range(0, 25);
            char assignedLetter = letters[randomNo];

            // Seperate section and rule of the selected letter that looks at if the letter is in any lit indiators
            // if (above is true)
            //      condition = 7;

            // If the letter is found in a lit indicator
            if (condition == 7) {
                Debug.LogFormat("[Mechanical Switches #{0}] Key no. {1}'s light is flashing {2} in Morse Code, and {2} is found in at least one of the bomb's lit indiators. Tap the key three times in two seconds before holding it.", moduleId, mechanicalKeys[i].GetNumber(), assignedLetter);
                mechanicalKeys[i].SetNeedRapidTapping(true);
            }

            // If the letter is not found in a lit indicator
            else {
                int tapTime = letterDigRoot[randomNo];

                mechanicalKeys[i].SetPressTime(tapTime);

                if (tapTime == Bomb.GetSerialNumberNumbers().Last()) {
                    Debug.LogFormat("[Mechanical Switches #{0}] Key no. {1}'s light is flashing {2} in Morse Code, and {2} is not found in any one of the bomb's lit indiators. The digital root of the number assigned to {2} is equal to the last digit of the serial number. Only interact with the key when the last two digits of the timer are {3}{2}.", moduleId, mechanicalKeys[i].GetNumber(), assignedLetter, tapTime, tapTime % 6);
                    mechanicalKeys[i].SetDoublePressTime(true);
                }

                else
                    Debug.LogFormat("[Mechanical Switches #{0}] Key no. {1}'s light is flashing {2} in Morse Code, and {2} is not found in any one of the bomb's lit indiators. The digital root of the number assigned to {2} is not equal to the last digit of the serial number. Only interact with the key when the last digit of the timer is a {2}.", moduleId, mechanicalKeys[i].GetNumber(), assignedLetter, tapTime);
            }

            mechanicalKeys[i].SetCondition(0);
            // StartCorutine(DisplayMorseCode(i, letterValues[randomNo]));
        }
    }

    // Waits for a minute
    private IEnumerator WaitMinute(int i) {
        yield return new WaitForSeconds(60.0f);
        mechanicalKeys[i].SetPressSafe(true);
    }

    // Rapidtapping
    private IEnumerator RapidTap(int i) {
        yield return null;
        // checks if the same key has been tapped three times within two seconds without being held
    }

    // Has the light flickering in Morse Code
    private IEnumerator DisplayMorseCode(int i, string letter) {
        yield return new WaitForSeconds(0.35f);

        // Lights[i].enabled = false;
        // flicker the lights in morse code with 0 = off, 1 = on for binary digits in the letters that loops through the length
        // repeat corutine until key is held
    }


    // Sets the keys
    private void SetKeys() {
        char[] keyboardText = { 'X', 'B', 'C', 'R', 'L' };
        int[] numbers = new int[5];
        int[] conditions = new int[5];

        // Sets the switches
        for (int i = 0; i < numbers.Length; i++)
            numbers[i] = UnityEngine.Random.Range(0, switches.Length);

        keyOrder = AssignNumbers(numbers);

        // Sets the conditions
        conditions = SetConditions();

        // Assigns the data to each key and logs it
        for (int i = 0; i < mechanicalKeys.Length; i++) {
            mechanicalKeys[i] = new MechanicalKey(Keys[i], switches[numbers[i]], Screens[i], conditions[i], keyOrder[i], keyboardText[i]);
            Debug.LogFormat("[Mechanical Switches #{0}] Key {1} has a {2} switch, and is assigned no. {3}.", moduleId, keyboardText[i], switches[numbers[i]].GetName(), keyOrder[i]);
        }
    }

    // Assigns the number to the switches
    private int[] AssignNumbers(int[] numbers) {
        int[] returningNumbers = new int[5];
        int currentNumber = 1;

        for (int i = 0; i < switches.Length; i++) {

            for (int j = 0; j < numbers.Length; j++) {
                if (numbers[j] == i) {
                    returningNumbers[j] = currentNumber;
                    currentNumber++;
                }
            }

            if (currentNumber > numbers.Length)
                break;
        }

        return returningNumbers;
    }

    // Sets the conditions for the keys
    private int[] SetConditions() {
        int[] conditions = new int[5];
        int randomNo;

        /* 0: Normal
         * 
         * For when the key is held
         * 1: Incorrect color
         * 2: Rapidfire flash
         * 3: No color
         * 
         * For after the key is tapped
         * 4: Constant lit
         * 5: Constant and wrong color
         * 6: Constant lit in Morse Code
         */

        for (int i = 0; i < conditions.Length; i++) {
            randomNo = UnityEngine.Random.Range(0, 30);

            switch (randomNo) {
                case 1: conditions[i] = 1; break;
                case 2: conditions[i] = 2; break;
                case 3: conditions[i] = 3; break;
                case 4: conditions[i] = 4; break;
                case 5: conditions[i] = 5; break;
                case 6: conditions[i] = 6; break;
                default: conditions[i] = 0; break;
            }
        }

        return conditions;
    }


    // Gets the order rule
    private int[] GetOrderRule() {
        int[] n = new int[5];

        // Rule variables
        int cherryCount = 0;
        int gateronCount = 0;
        int kailhCount = 0;
        int kailhBoxCount = 0;
        int kailhProCount = 0;
        int razerCount = 0;
        int blueCount = 0;
        int brownCount = 0;
        int greenCount = 0;
        int redCount = 0;
        int speedCount = 0;
        int tealiosCount = 0;
        int whiteCount = 0;
        int zealiosCount = 0;

        // Gets info for rule variables
        for (int i = 0; i < mechanicalKeys.Length; i++) {
            if (mechanicalKeys[i].GetKeySwitch().GetBrand() == "Cherry")
                cherryCount++;

            if (mechanicalKeys[i].GetKeySwitch().GetBrand() == "Gateron")
                gateronCount++;

            if (mechanicalKeys[i].GetKeySwitch().GetBrand() == "Kailh")
                kailhCount++;

            if (mechanicalKeys[i].GetKeySwitch().GetBrand() == "Kailh Box")
                kailhBoxCount++;

            if (mechanicalKeys[i].GetKeySwitch().GetBrand() == "Kailh Pro")
                kailhProCount++;

            if (mechanicalKeys[i].GetKeySwitch().GetBrand() == "Razer")
                razerCount++;

            if (mechanicalKeys[i].GetKeySwitch().GetColor() == "Blue" || mechanicalKeys[i].GetKeySwitch().GetColor() == "Pale Blue")
                blueCount++;

            if (mechanicalKeys[i].GetKeySwitch().GetColor() == "Brown")
                brownCount++;

            if (mechanicalKeys[i].GetKeySwitch().GetColor() == "Green")
                greenCount++;

            if (mechanicalKeys[i].GetKeySwitch().GetColor() == "Red")
                redCount++;

            if (mechanicalKeys[i].GetKeySwitch().GetColor() == "Silver" || mechanicalKeys[i].GetKeySwitch().GetColor() == "Bronze" || mechanicalKeys[i].GetKeySwitch().GetColor() == "Gold" || mechanicalKeys[i].GetKeySwitch().GetColor() == "Copper")
                speedCount++;

            if (mechanicalKeys[i].GetKeySwitch().GetColor() == "Tealios")
                tealiosCount++;

            if (mechanicalKeys[i].GetKeySwitch().GetColor() == "White")
                whiteCount++;

            if (mechanicalKeys[i].GetKeySwitch().GetColor() == "Zealios")
                zealiosCount++;
        }

        // Selects rule

        if (cherryCount == 5) { n[0] = 1; n[1] = 2; n[2] = 3; n[3] = 4; n[4] = 5; } // If all the switches are Cherry switches.                 1 2 3 4 5
        else if (redCount == 2) { n[0] = 2; n[1] = 4; n[2] = 5; n[3] = 3; n[4] = 1; } // If there are exactly two red switches.                 2 4 5 3 1
        else if (blueCount == 3) { n[0] = 1; n[1] = 4; n[2] = 5; n[3] = 2; n[4] = 3; } // If there are exactly three blue switches.             1 4 5 2 3
        else if (speedCount == 1) { n[0] = 5; n[1] = 2; n[2] = 1; n[3] = 4; n[4] = 3; } // If there is only one Speed switch.                   5 2 1 4 3
        else if (brownCount == 4) { n[0] = 4; n[1] = 1; n[2] = 2; n[3] = 3; n[4] = 5; } // If there are exactly four brown switches.            4 1 2 3 5
        else if (razerCount == 2) { n[0] = 2; n[1] = 3; n[2] = 5; n[3] = 4; n[4] = 1; } // If there are exactly two Razer switches.             2 3 5 4 1
        else if (whiteCount == 1) { n[0] = 4; n[1] = 1; n[2] = 5; n[3] = 2; n[4] = 3; } // If there is only one white switch.                   4 1 5 2 3
        else if (kailhBoxCount == 3) { n[0] = 1; n[1] = 5; n[2] = 2; n[3] = 3; n[4] = 4; } // If there are exactly three Kailh Box switches.	1 5 2 3 4
        else if (greenCount == 3) { n[0] = 2; n[1] = 5; n[2] = 3; n[3] = 4; n[4] = 1; } // If there are exactly three green switches.           2 5 3 4 1
        else if (razerCount > 2) { n[0] = 3; n[1] = 4; n[2] = 1; n[3] = 5; n[4] = 2; } // If there are more than two Razer switches.	        3 4 1 5 2
        else if (gateronCount + kailhProCount > 2) { n[0] = 2; n[1] = 3; n[2] = 1; n[3] = 4; n[4] = 5; } // If more than half of the switches are Gateron and Kailh Pro.	        2 3 1 4 5
        else if (gateronCount == 1) { n[0] = 4; n[1] = 3; n[2] = 5; n[3] = 1; n[4] = 2; } // If there is exactly on Gateron switch.             4 3 5 1 2
        else if (kailhBoxCount == 5) { n[0] = 3; n[1] = 1; n[2] = 2; n[3] = 4; n[4] = 5; } // If all the switches are Kailh Box switches.	    3 1 2 4 5
        else if (gateronCount == 4) { n[0] = 2; n[1] = 4; n[2] = 1; n[3] = 3; n[4] = 5; } // If there are exactly four Gateron switches.        2 4 1 3 5
        else if (tealiosCount >= 1 && zealiosCount >= 1) { n[0] = 1; n[1] = 5; n[2] = 4; n[3] = 3; n[4] = 2; } // If there are both at least one Tealios and one Zealios switch.    1 5 4 3 2
        else if (cherryCount < 2 && gateronCount < 2 && kailhCount < 2 && kailhBoxCount < 2 && kailhProCount < 2 && razerCount < 2) { n[0] = 4; n[1] = 2; n[2] = 3; n[3] = 1; n[4] = 5; } // If all the switches are a different category.	4 2 3 1 5
        else { n[0] = 5; n[1] = 4; n[2] = 3; n[3] = 2; n[4] = 1; } // Otherwise: 5 4 3 2 1

        Debug.LogFormat("[Mechanical Switches #{0}] The key order selected is {1}, {2}, {3}, {4}, {5}", moduleId, n[0], n[1], n[2], n[3], n[4]);

        return n;
    }


    // Converts serial number characters into directions
    private void ConvertSerialNumber() {
        bool[] serialNumberConverted = new bool[6];

        for (int i = 0; i < serialNumber.Length; i++) {
            serialNumberConverted[i] = NumeralConversion(serialNumber[i]);
        }

        for (int i = 0; i < serialNumberConversions.Length; i++) {
            serialNumberConversions[i] = serialNumberConverted[i % serialNumber.Length];
        }
    }

    // Converts letters to numbers, takes the digital root, and converts to booleans
    private bool NumeralConversion(char c) {
        switch(c) {
            case '0': return false;
            case '1': return false;
            case '2': return false;
            case '3': return false;
            case '4': return false;
            case '5': return true;
            case '6': return true;
            case '7': return true;
            case '8': return true;
            case '9': return true;
            case 'A': return false;
            case 'B': return false;
            case 'C': return false;
            case 'D': return false;
            case 'E': return true;
            case 'F': return true;
            case 'G': return true;
            case 'H': return true;
            case 'I': return true;
            case 'J': return false;
            case 'K': return false;
            case 'L': return false;
            case 'M': return false;
            case 'N': return true;
            case 'O': return true;
            case 'P': return true;
            case 'Q': return true;
            case 'R': return true;
            case 'S': return false;
            case 'T': return false;
            case 'U': return false;
            case 'V': return false;
            case 'W': return true;
            case 'X': return true;
            case 'Y': return true;
            case 'Z': return true;
            default: return false;
        }
    }

    // Sets the rules not requiring switches
    private void SetRules() {
        // Resets rules
        for (int i = 0; i < rules.Length; i++)
            rules[i] = false;

        if (Bomb.GetOnIndicators().Count() >= 2) { rules[0] = true; } // 1. The bomb has at least two lit indicators
        if (Bomb.GetSerialNumberLetters().Any(x => x == 'A' || x == 'E' || x == 'I' || x == 'O' || x == 'U')) { rules[1] = true; } // 2. The bomb has a vowel in its serial number
        if (Bomb.GetModuleNames().Count() % 2 == 0) { rules[3] = true; } // 4. The bomb has an even number of modules
        if (DateTime.Now.DayOfWeek.ToString() == "Sunday") { rules[4] = true; } // 5. The bomb was initiated on a Sunday
        if (Bomb.GetSerialNumberNumbers().Distinct().Count() >= 3) { rules[5] = true; } // 6. The bomb has at least three unique numbers in its serial number
        if (Bomb.GetSolvableModuleNames().Count(x => x.Contains("Simon")) >= 2) { rules[8] = true; } // 9. The bomb has at least two modules with 'Simon' in its name
        if (Bomb.GetIndicators().Count() == 1) { rules[9] = true; } // 10.The bomb has exactly one indicator
        if (Bomb.GetBatteryCount() % 2 == 0) { rules[10] = true; } // 11.The bomb has an even number of batteries
        if (Bomb.GetPortCount(Port.DVI) % 2 == 1) { rules[11] = true; } // 12.The bomb has an odd amount of DVI - D ports
        if (EmptyPortPlate() == true) { rules[12] = true; }// 13.The bomb has an empty port plate
        if (Bomb.GetPortCount() == 0) { rules[14] = true; } // 15.There are no ports present on the bomb
        if (Bomb.GetPortPlateCount() == 0) { rules[15] = true; } // 16.There are no port plates present on the bomb
        if (!Bomb.GetSolvableModuleNames().Any(x => x.Contains("Simon"))) { rules[18] = true; } // 19.The bomb does not have a module with 'Simon' in its name
        if (Bomb.GetSolvableModuleNames().Count(x => x.Contains("Mechanical Switches")) == 1) // 20.The bomb has exactly one Mechanical Switches module
        if (Bomb.GetBatteryCount() % 2 == 1) { rules[20] = true; } // 21.The bomb has an odd number of batteries
        if (Bomb.GetSolvableModuleNames().Count(x => x.Contains("Piano Keys")) >= 1) { rules[21] = true; } // 22.The bomb has at least one module with 'Piano Keys' in its name
        if (Bomb.GetSolvableModuleNames().Contains("Turn The Key") || Bomb.GetSolvableModuleNames().Contains("Forget Me Not")) { rules[22] = true; } // 23.The bomb has at least one Turn The Key or Forget Me Not module
        if (Bomb.GetPortPlateCount() == 3) { rules[23] = true; } // 24.The bomb has exactly three port plates
        if (Bomb.GetIndicators().Count() % 2 == 1) { rules[29] = true; } // 30.The bomb has an odd number of indicators
        if (Bomb.GetPortCount(Port.Parallel) % 2 == 1) { rules[30] = true; } // 31.The bomb has an odd number of parallel ports
        if (Bomb.GetSerialNumberNumbers().Any(x => x == '0' || x == '2' || x == '4' || x == '6' || x == '8')) { rules[32] = true; } // 33.The bomb has an even number in its serial number
        if (Bomb.GetBatteryCount() == 2) { rules[33] = true; } // 34.The bomb has exactly two batteries
        if (Bomb.GetSerialNumberNumbers().Any(x => x == '0')) { rules[35] = true; } // 36.The bomb has a zero in its serial number
        if (Bomb.GetBatteryCount() == 1) { rules[37] = true; } // 38.The bomb has exactly one battery
        if (Bomb.GetPortCount() == 5) { rules[38] = true; } // 39.The bomb has exactly five ports
        if (Bomb.GetIndicators().Count() == 3) { rules[39] = true; } // 40.The bomb has exactly three indicators
        if (Bomb.GetSolvableModuleNames().Count(x => x.Contains("Souvenir")) == 1) { rules[40] = true; } // 41.The bomb has exactly one Souvenir module
    }

    // Updates rules for switches
    private void UpdateRules(KeySwitch key, int number) {
        if (key.GetBrand() == "Kailh Pro") { rules[2] = true; } else { rules[2] = false; } // 3. This key is a Kailh Pro key
        if (key.GetBrand() == "Cherry") { rules[6] = true; } else { rules[6] = false; } // 7. This key a Cherry key
        if (key.GetColor() == "Red") { rules[7] = true; } else { rules[7] = false; } // 8. This key is red
        if (key.GetColor() == "Blue" || key.GetColor() == "Pale Blue") { rules[13] = true; } else { rules[13] = false; } // 14.This key is blue
        if (key.GetBrand() == "Kailh Box") { rules[16] = true; } else { rules[16] = false; } // 17.This key is a Kailh Box key
        if (key.GetColor() == "Clear") { rules[17] = true; } else { rules[17] = false; } // 18.This key is clear
        if (key.GetBrand() == "Gateron") { rules[24] = true; } else { rules[24] = false; } // 25.This key is a Gateron key
        if (number == 3 || number == 4) { rules[25] = true; } else { rules[25] = false; } // 26.This key's number is 3 or 4
        if (number == 2) { rules[26] = true; } else { rules[26] = false; } // 27.This key's number is 2
        if (number == 3) { rules[27] = true; } else { rules[27] = false; } // 28.This key's number is 3
        if (number == 2 || number == 5) { rules[28] = true; } else { rules[28] = false; } // 29.This key's number is 2 or 5
        if (key.GetColor() == "Purple" || key.GetColor() == "Yellow") { rules[31] = true; } else { rules[31] = false; } // 32.This key is either purple or yellow
        if (key.GetBrand() == "Razer") { rules[34] = true; } else { rules[34] = false; } // 35.This key is a Razer key
        if (key.GetColor() == "Brown" || key.GetColor() == "Black") { rules[36] = true; } else { rules[36] = false; } // 37.This is key either brown or black
    }

    // If there is an empty port plate
    private bool EmptyPortPlate() {
        bool empty = false;

        foreach (object[] plate in Bomb.GetPortPlates()) {
            if (plate.Length == 0) {
                empty = true;
                break;
            }
        }

        return empty;
    }


    // Gets the key hold order
    private void GetHoldOrder() {
        InitiateGrid();
        SetStartPos();
        MoveKeys();
        GetOrderSequence();
    }

    // Moves the keys on Table 3
    private void MoveKeys() {
        for (iterations = 1; iterations <= 10; iterations++) {
            int ruleNo = 0;
            int loopCounter = 0;
            bool lastRuleTrue = false;
            bool moved = false;
            int[] directions = { 2, 2, 2, 2 };
            int[] previousPos = new int[2];
            int[] newPos = new int[2];
            ConvertSerialNumber();

            for (int i = 1; i <= mechanicalKeys.Length; i++) {
                loopBreak = false;
                
                for (int j = 0; j < mechanicalKeys.Length && loopBreak == false; j++) {        

                    if (mechanicalKeys[j].GetNumber() == i) {
                        UpdateRules(mechanicalKeys[j].GetKeySwitch(), mechanicalKeys[j].GetNumber());
                        lastRuleTrue = false;

                        for (int k = 1; k <= i; k++) {
                            moved = false;
                            loopCounter = 1;

                            while (moved == false) {
                                previousPos = mechanicalKeys[j].GetGridPos();

                                directions = ChecksOpenPos(j, previousPos);
                                /* 0: DL
                                 * 1: DR
                                 * 2: UL
                                 * 3: UR
                                 */

                                // If the key can't move at all or if the grid has rotated four times for this movement
                                if ((directions[0] < 2 && directions[1] < 2 && directions[2] < 2 && directions[3] < 2) ||
                                    loopCounter > 4) {

                                    moved = true;
                                }

                                // If the key can't move down and the last rule was true
                                else if (directions[0] < 2 && directions[1] < 2
                                    && lastRuleTrue == true) {

                                    mechanicalKeys[j].SetRotation(mechanicalKeys[j].GetRotation() + 1 % 4); // Rotate clock
                                    loopCounter++;
                                }

                                // If the key can't move down
                                else if (directions[0] < 2 && directions[1] < 2) {

                                    mechanicalKeys[j].SetRotation(mechanicalKeys[j].GetRotation() + 2 % 4); // Rotate 180
                                    loopCounter++;
                                }

                                // If the key can't move in the specified direction due to a nonexistant space
                                else if ((serialNumberConversions[ruleNo] == false && directions[0] == 0) ||
                                    (serialNumberConversions[ruleNo] == true && directions[1] == 0)) {

                                    if (serialNumberConversions[ruleNo] == false)
                                        serialNumberConversions[ruleNo] = true;

                                    else
                                        serialNumberConversions[ruleNo] = false;

                                    loopCounter++;
                                }

                                // If the key can't move in the specified direction due to an occupied space
                                else if ((serialNumberConversions[ruleNo] == false && directions[0] == 1) ||
                                    (serialNumberConversions[ruleNo] == true && directions[1] == 1)) {

                                    mechanicalKeys[j].SetRotation(mechanicalKeys[j].GetRotation() + 3 % 4); // Rotate counter
                                    loopCounter++;
                                }


                                // Key moves
                                else {
                                    // Down-right
                                    if (serialNumberConversions[ruleNo] == true)
                                        newPos = MoveKeyDownRight(j, mechanicalKeys[j].GetRotation(), previousPos);

                                    // Down-left
                                    else
                                        newPos = MoveKeyDownLeft(j, mechanicalKeys[j].GetRotation(), previousPos);


                                    tableGridPos[newPos[0]][newPos[1]] = i;
                                    tableGridPos[previousPos[0]][previousPos[1]] = 0;
                                    moved = true;

                                    UpdateGridPos(j);

                                    // Checks rule for the space
                                    if (rules[tableGrid[newPos[0]][newPos[1]] - 1] == true) {
                                        lastRuleTrue = true;

                                        if (k == i)
                                            mechanicalKeys[j].SetRotation(mechanicalKeys[j].GetRotation() + 3 % 4); // Rotate counter

                                        else
                                            mechanicalKeys[j].SetRotation(mechanicalKeys[j].GetRotation() + 1 % 4); // Rotate clock
                                    }

                                    else
                                        lastRuleTrue = false;
                                }
                            }

                            ruleNo++;
                        }

                        loopBreak = true;
                    }
                }
            }

            LogKeyPositions();
        }
    }


    // Sets the positions of the keys on the grid
    private void SetStartPos() {
        for (int i = 0; i < mechanicalKeys.Length; i++) {
            loopBreak = false;

            for (int j = 1; j <= mechanicalKeys.Length && loopBreak == false; j++) {

                if (mechanicalKeys[i].GetNumber() == j) {

                    for (int k = 0; k < keyOrder.Length && loopBreak == false; k++) {

                        if (keyOrder[k] == j) {

                            tableGridPos[0][k * 2] = j;
                            int[] newGridPos = { 0, k * 2 };
                            mechanicalKeys[i].SetGridPos(newGridPos);
                            loopBreak = true;
                        }
                    }
                }
            }
        }

        LogKeyPositions();
    }

    // Updates the key's position on the grid
    private void UpdateGridPos(int i) {
        loopBreak = false;

        for (int k = 0; k < 9 && loopBreak == false; k++) {

            for (int l = 0; l < 9 && loopBreak == false; l++) {

                if (tableGridPos[k][l] == mechanicalKeys[i].GetNumber()) {

                    int[] receivedGridNumbers = { k, l };
                    mechanicalKeys[i].SetGridPos(receivedGridNumbers);
                    loopBreak = true;
                }
            }
        }
    }

    // Gets the positions of each key in reading order
    private void GetOrderSequence() {
        int keysFound = 0;
        loopBreak = false;

        for (int i = 0; i < 9 && loopBreak == false; i++) {

            for (int j = 0; j < 9 && loopBreak == false; j++) {

                if (tableGridPos[i][j] != 0) {
                    holdOrder[keysFound] = tableGridPos[i][j];
                    keysFound++;

                    if (keysFound > 4)
                        loopBreak = true;
                }
            }
        }

        Debug.LogFormat("[Mechanical Switches #{0}] The hold order selected is {1}, {2}, {3}, {4}, {5}", moduleId, holdOrder[0], holdOrder[1], holdOrder[2], holdOrder[3], holdOrder[4]);
    }

    // Logs the key's positions
    private void LogKeyPositions() {
        string iterationPlural = "";

        // Writes plural or singular form of 'iteration'
        if (iterations == 1)
            iterationPlural = "iteration";

        else
            iterationPlural = "iterations";

        Debug.LogFormat("[Mechanical Switches #{0}] After {1} {2}, " +
            "Key {3} is in {4}{5} and rotated {6}, " +
            "Key {7} is in {8}{9}, and rotated {10}, " +
            "Key {11} is in {12}{13} and rotated {14}, " +
            "Key {15} is in {16}{17} and rotated {18}, and " +
            "Key {19} is in {20}{21} and rotated {22}.",
            moduleId, iterations, iterationPlural,
            mechanicalKeys[0].GetNumber(), mechanicalKeys[0].GetGridPosLogger0(), mechanicalKeys[0].GetGridPosLogger1(), mechanicalKeys[0].GetRotationLogger(),
            mechanicalKeys[1].GetNumber(), mechanicalKeys[1].GetGridPosLogger0(), mechanicalKeys[1].GetGridPosLogger1(), mechanicalKeys[1].GetRotationLogger(),
            mechanicalKeys[2].GetNumber(), mechanicalKeys[2].GetGridPosLogger0(), mechanicalKeys[2].GetGridPosLogger1(), mechanicalKeys[2].GetRotationLogger(),
            mechanicalKeys[3].GetNumber(), mechanicalKeys[3].GetGridPosLogger0(), mechanicalKeys[3].GetGridPosLogger1(), mechanicalKeys[3].GetRotationLogger(),
            mechanicalKeys[4].GetNumber(), mechanicalKeys[4].GetGridPosLogger0(), mechanicalKeys[4].GetGridPosLogger1(), mechanicalKeys[4].GetRotationLogger());
    }

    
    // Creates the grid
    private void InitiateGrid() {
        for (int i = 0; i < 9; i++) {
            tableGrid[i] = new int[9];
            tableGridPos[i] = new int[9];

            for (int j = 0; j < 9; j++) {
                tableGrid[i][j] = 0;
                tableGridPos[i][j] = 0;
            }
        }

        tableGrid[0][0] = 32;
        tableGrid[0][2] = 9;
        tableGrid[0][4] = 40;
        tableGrid[0][6] = 2;
        tableGrid[0][8] = 11;

        tableGrid[1][1] = 8;
        tableGrid[1][3] = 12;
        tableGrid[1][5] = 4;
        tableGrid[1][7] = 41;

        tableGrid[2][0] = 15;
        tableGrid[2][2] = 39;
        tableGrid[2][4] = 25;
        tableGrid[2][6] = 30;
        tableGrid[2][8] = 35;

        tableGrid[3][1] = 20;
        tableGrid[3][3] = 18;
        tableGrid[3][5] = 22;
        tableGrid[3][7] = 17;

        tableGrid[4][0] = 19;
        tableGrid[4][2] = 31;
        tableGrid[4][4] = 26;
        tableGrid[4][6] = 37;
        tableGrid[4][8] = 24;

        tableGrid[5][1] = 36;
        tableGrid[5][3] = 7;
        tableGrid[5][5] = 5;
        tableGrid[5][7] = 33;

        tableGrid[6][0] = 16;
        tableGrid[6][2] = 38;
        tableGrid[6][4] = 27;
        tableGrid[6][6] = 21;
        tableGrid[6][8] = 1;

        tableGrid[7][1] = 3;
        tableGrid[7][3] = 13;
        tableGrid[7][5] = 28;
        tableGrid[7][7] = 10;

        tableGrid[8][0] = 29;
        tableGrid[8][2] = 6;
        tableGrid[8][4] = 14;
        tableGrid[8][6] = 34;
        tableGrid[8][8] = 23;
    }

    // Tests positions
    public int[] ChecksOpenPos(int i, int[] previousPos) {
        int[] directions = { 2, 2, 2, 2 };

        Func<int[], bool>[] func = {
            (x) => x[0] + 1 > 8 || x[1] - 1 < 0,
            (x) => x[0] + 1 > 8 || x[1] + 1 > 8,
            (x) => x[0] - 1 < 0 || x[1] - 1 < 0,
            (x) => x[0] - 1 < 0 || x[1] + 1 > 8
        };

        Func<int[], bool>[] gridPos = {
            (x) => tableGridPos[x[0] + 1][x[1] - 1] != 0,
            (x) => tableGridPos[x[0] + 1][x[1] + 1] != 0,
            (x) => tableGridPos[x[0] - 1][x[1] - 1] != 0,
            (x) => tableGridPos[x[0] - 1][x[1] + 1] != 0
        };

        for (int j = 0; j < 4; j++) {
            int rot = (mechanicalKeys[i].GetRotation() + j) % 4;
            if (func[rot](previousPos))
                directions[j] = 0;
            else if (gridPos[rot](previousPos))
                directions[j] = 1;
        }

        return directions;
    }

    // Moves the keys
    public int[] MoveKeyDownLeft(int i, int rotation, int[] previousPos) {
        int[] newPos = { 0, 0 };

        /* For rotation north
         * 
         * newPos[0] = previousPos[0] + 1;
         * newPos[1] = previousPos[1] - 1;
         */
        
        switch (rotation) {
            case 0: // North
                newPos[0] = previousPos[0] + 1;
                newPos[1] = previousPos[1] - 1;
                break;

            case 1: // East
                newPos[0] = previousPos[0] - 1;
                newPos[1] = previousPos[1] - 1;
                break;

            case 2: // South
                newPos[0] = previousPos[0] - 1;
                newPos[1] = previousPos[1] + 1;
                break;

            case 3: // West
                newPos[0] = previousPos[0] + 1;
                newPos[1] = previousPos[1] + 1;
                break;

            default:
                newPos[0] = previousPos[0] + 1;
                newPos[1] = previousPos[1] - 1;
                break;
        }

        return newPos;
    }

    public int[] MoveKeyDownRight(int i, int rotation, int[] previousPos) {
        int[] newPos = { 0, 0 };

        /* For rotation north
         * 
         * newPos[0] = previousPos[0] + 1;
         * newPos[1] = previousPos[1] + 1;
         */

        switch (rotation) {
            case 0: // North
                newPos[0] = previousPos[0] + 1;
                newPos[1] = previousPos[1] + 1;
                break;

            case 1: // East
                newPos[0] = previousPos[0] + 1;
                newPos[1] = previousPos[1] - 1;
                break;

            case 2: // South
                newPos[0] = previousPos[0] - 1;
                newPos[1] = previousPos[1] - 1;
                break;

            case 3: // West
                newPos[0] = previousPos[0] - 1;
                newPos[1] = previousPos[1] + 1;
                break;

            default:
                newPos[0] = previousPos[0] + 1;
                newPos[1] = previousPos[1] + 1;
                break;
        }

        return newPos;
    }


    // Colorblind mode
        /* If (colorblind mode is on) {
         *      display the colors of the switches somewhere around the switches (not on the screen)
         * }
         */


    // Twitch Plays
#pragma warning disable 414
    private string TwitchHelpMessage = "!{0} cycle [taps the keys in reading order] | !{0} tap/hold 1 [taps/holds first key in reading order anytime] | !{0} tap/hold 1 on 5 [taps/holds first key in reading order when last digit of the timer is a 5] | !{0} tap/hold 1 at 55 [taps/holds first key in reading order when last two digits of the timer are 55] | !{0} rapidtap 1 [taps first key in reading order rapidly for two seconds] | !{0} release [releases the held key anytime] | !{0} release on 5 [relases the held key when last digit of the timer is a 5]"; // Add " | !{0} colorblind [turns colorblind mode on]" when colorblind mode is implmented
#pragma warning restore 414

    // Commands
    private IEnumerator ProcessTwitchCommand(string command) {
        yield return null; // Placeholder for now

        /* Commands:
         * 
         *          When refering to 0.5 seconds in this section - it means long enough that it doesn't count as a hold
         * 
         * !{0} cycle - taps each of the keys in reading order for 0.5 seconds each
         * 
         * !{0} tap {1} - taps key no. {1} in reading order for 0.5 seconds
         * !{0} tap {1} on {2} - taps key no. {1} in reading order for 0.5 seconds when last digit of timer is {2}
         * !{0} tap {1} at {2} - taps key no. {1} in reading order for 0.5 seconds when last two digits of timer are {2}
         * 
         * !{0} rapidtap {1} - taps key no. {1} in reading order three times in the duration of two seconds
         * 
         * !{0} hold {1} - holds key no. {1} in reading order
         * !{0} hold {1} on {2} - holds key no. {1} in reading order when last digit of timer is {2}
         * !{0} hold {1} at {2] - holds key no. {1} in reading order when last two digits of timer are {2}
         * 
         * !{0} release - releases the held key
         * !{0} release on {1} - releases the held key when last digit of timer is {1}
         * 
         * !{0} colorblind - toggles colorblind mode (when it gets implemented)
         */
    }
}