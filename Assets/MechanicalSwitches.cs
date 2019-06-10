using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KModkit;

/* Things left to implement:
 * 
 * Keys with the possibility of going faulty (Page 6 - 7 info)
 * Add a big screen that displays the information of the switch when it is tapped 
 * Change the font of the keys
 * Change the material of the keys
 * Change the background of the module
 * Create lights on the screens that can be toggled
 * Make the module solvable
 * Create an SVG for the manual
 * Fix an infinite loop in MoveKeys()
 * Make TP code
 * Detect lengths of holding the keys
 * Animate the key pressing
 * Get sounds for keyboard clicks
 * Write code that tests for if any unlit indicators match a letter in a switch's name
 * Redo the manual's PDF
 * Add colorblind mode
 * Add a preview image
 */

// Repo module desc: "Select each button and figure out which keys make which sounds. Using these sounds, figure out when to hold each button down and when you should release them. Watch out for faulty keys!"

public class MechanicalSwitches : MonoBehaviour {
    public KMAudio Audio;
    public KMBombInfo Bomb;
    public KMBombModule Module;

    public KMSelectable[] Keys;
    public Renderer[] Screens;
    public Material[] Colors;

    // Logging info
    private static int moduleIdCounter = 1;
    private int moduleId;
    private bool moduleSolved = false;

    // Solving info
    private MechanicalKey[] mechanicalKeys = new MechanicalKey[5];
    private bool[] rules = new bool[41];
    private KeySwitch[] switches = new KeySwitch[39];
    private int[] keyOrder = new int[5];
    private int[] holdOrder = new int[5];

    // Table grid info
    private int[][] tableGrid = new int[9][];
    private int[][] tableGridPos = new int[9][];
    private char[] serialNumber = new char[6];
    private bool[] serialNumberConversions = new bool[15];
    private int iterations = 0;
    private bool[] indicatorMatch = new bool[5];

    private int gridRotation = 0;
    /* 0 = North
     * 1 = East
     * 2 = South
     * 3 = East
     */ 

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
        }
    }

    // Gets module ready
    private void Start () {
        SetRules();
        SetSwitches();

        // Turns serial number into movement directions
        serialNumber = Bomb.GetSerialNumber().ToCharArray();
        ConvertSerialNumber();

        Module.OnActivate += OnActivate;
    }

    // Runs when lights turn on
    private void OnActivate() {
        ModuleReset();
    }
	
	// Currently unused - probably not for long
	private void Update () {
		// Ran once every frame
	}


    // Module resets and/or starts
    private void ModuleReset() {
        iterations = 0;
        SetKeys();
        keyOrder = GetOrderRule();
        GetHoldOrder();
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

        float zealiosRandom = UnityEngine.Random.Range(62.0f, 78.0f);
        float aliazRandom = UnityEngine.Random.Range(60.0f, 100.0f);

        switches[0] = new KeySwitch("Cherry Black", "Cherry", "Black", Colors[1], "Linear", 60.0f, 2.0f, 4.0f); // Cherry Black
        switches[1] = new KeySwitch("Cherry Speed Silver", "Cherry", "Speed Silver", Colors[23], "Linear", 45.0f, 1.2f, 3.4f); // Cherry Speed Silver
        switches[2] = new KeySwitch("Gateron Black", "Gateron", "Black", Colors[2], "Linear", 50.0f, 2.0f, 4.0f); // Gateron Black
        switches[3] = new KeySwitch("Kailh Box Black", "Kailh Box", "Black", Colors[2], "Linear", 60.0f, 1.8f, 3.6f); // Kailh Box Black
        switches[4] = new KeySwitch("Kailh Pro Burgundy", "Kailh Pro", "Burgundy", Colors[6], "Linear", 50.0f, 1.7f, 3.6f); // Kailh Pro Burgundy
        switches[5] = new KeySwitch("Cherry Red", "Cherry", "Red", Colors[22], "Linear", 45.0f, 2.0f, 4.0f); // Cherry Red
        switches[6] = new KeySwitch("Gateron Red", "Gateron", "Red", Colors[22], "Linear", 50.0f, 2.0f, 4.0f); // Gateron Red
        switches[7] = new KeySwitch("Kailh Box Red", "Kailh Box", "Red", Colors[22], "Linear", 45.0f, 1.8f, 3.6f); // Kailh Box Red
        switches[8] = new KeySwitch("Kailh Pro Purple", "Kailh Pro", "Purple", Colors[21], "Tactile", 50.0f, 1.7f, 3.6f); // Kailh Pro Purple
        switches[9] = new KeySwitch("Cherry Brown", "Cherry", "Brown", Colors[5], "Tactile", 45.0f, 2.0f, 4.0f); // Cherry Brown
        switches[10] = new KeySwitch("Gateron Brown", "Gateron", "Brown", Colors[5], "Tactile", 50.0f, 2.0f, 4.0f); // Gateron Brown
        switches[11] = new KeySwitch("Kailh Box Brown", "Kailh Box", "Brown", Colors[5], "Tactile", 60.0f, 1.8f, 3.6f); // Kailh Box Brown
        switches[12] = new KeySwitch("Kalih Pro Green", "Kailh Pro", "Green", Colors[14], "Clicky", 50.0f, 1.7f, 3.6f); // Kailh Pro Green
        switches[13] = new KeySwitch("Cherry Blue", "Cherry", "Blue", Colors[3], "Clicky", 50.0f, 2.2f, 4.0f); // Cherry Blue
        switches[14] = new KeySwitch("Gateron Blue", "Gateron", "Blue", Colors[3], "Clicky", 55.0f, 2.2f, 4.0f); // Gateron Blue
        switches[15] = new KeySwitch("Kailh Box White", "Kailh Box", "White", Colors[25], "Clicky", 55.0f, 1.8f, 3.6f); // Kailh Box White
        switches[16] = new KeySwitch("Kailh Speed Silver", "Kailh", "Speed Silver", Colors[23], "Linear", 50.0f, 1.1f, 3.5f); // Kailh Speed Silver
        switches[17] = new KeySwitch("Cherry Clear", "Cherry", "Clear", Colors[8], "Tactile", 65.0f, 2.0f, 4.0f); // Cherry Clear
        switches[18] = new KeySwitch("Gateron Clear", "Gateron", "Clear", Colors[9], "Linear", 35.0f, 2.0f, 4.0f); // Gateron Clear
        switches[19] = new KeySwitch("Kailh Box Navy", "Kailh Box", "Navy", Colors[18], "Clicky", 75.0f, 1.7f, 3.6f); // Kailh Box Navy
        switches[20] = new KeySwitch("Kailh Speed Copper", "Kailh", "Copper", Colors[10], "Tactile", 50.0f, 1.1f, 3.5f); // Kailh Speed Copper
        switches[21] = new KeySwitch("Cherry White", "Cherry", "White", Colors[25], "Clicky", 85.0f, 2.0f, 4.0f); // Cherry White
        switches[22] = new KeySwitch("Gateron Yellow", "Gateron", "Yellow", Colors[26], "Linear", 50.0f, 2.0f, 4.0f); // Gateron Yellow
        switches[23] = new KeySwitch("Kailh Box Jade", "Kailh Box", "Jade", Colors[17], "Clicky", 65.0f, 1.7f, 3.6f); // Kailh Box Jade
        switches[24] = new KeySwitch("Kailh Speed Bronze", "Kailh", "Bronze", Colors[4], "Clicky", 50.0f, 1.1f, 3.5f); // Kailh Speed Bronze
        switches[25] = new KeySwitch("Cherry Green", "Cherry", "Green", Colors[13], "Tactile", 80.0f, 2.2f, 4.0f); // Cherry Green
        switches[26] = new KeySwitch("Gateron Green", "Gateron", "Green", Colors[13], "Clicky", 80.0f, 2.0f, 4.0f); // Gateron Green
        switches[27] = new KeySwitch("Kailh Box Dark Yellow", "Kailh Box", "Dark Yellow", Colors[11], "Linear", 70.0f, 1.8f, 3.6f); // Kailh Box Dark Yellow
        switches[28] = new KeySwitch("Kailh Speed Gold", "Kailh", "Speed Gold", Colors[12], "Clicky", 50.0f, 1.4f, 3.5f); // Kailh Speed Gold
        switches[29] = new KeySwitch("Razer Green", "Razer", "Green", Colors[15], "Clicky", 50.0f, 1.9f, 4.0f); // Razer Green
        switches[30] = new KeySwitch("Cherry Grey (Linear)", "Cherry", "Grey", Colors[16], "Linear", 80.0f, 2.0f, 4.0f); // Cherry Grey (Linear)
        switches[31] = new KeySwitch("Gateron Tealios", "Gateron", "Tealios", Colors[24], "Linear", 67.0f, 0.0f, 0.0f); // Gateron Tealios
        switches[32] = new KeySwitch("Kailh Box Burnt Orange", "Kailh Box", "Burnt Orange", Colors[7], "Tactile", 70.0f, 1.8f, 3.6f); // Kailh Box Burnt Orange
        switches[33] = new KeySwitch("Razer Orange", "Razer", "Orange", Colors[19], "Silent", 45.0f, 1.9f, 4.0f); // Razer Orange
        switches[34] = new KeySwitch("Cherry Grey (Tactile)", "Cherry", "Grey", Colors[16], "Tactile", 80.0f, 2.0f, 4.0f); // Cherry Grey (Tactile)
        switches[35] = new KeySwitch("Gateron Zealios", "Gateron", "Zealios", Colors[27], "Tactile", zealiosRandom, 0.0f, 0.0f); // Gateron Zealios
        switches[36] = new KeySwitch("Gateron Aliaz", "Gateron", "Aliaz", Colors[0], "Silent", aliazRandom, 2.0f, 4.0f); // Gateron Aliaz
        switches[37] = new KeySwitch("Kailh Box Pale Blue", "Kailh Box", "Pale Blue", Colors[20], "Clicky", 70.0f, 1.8f, 3.6f); // Kailh Box Pale Blue
        switches[38] = new KeySwitch("Razer Yellow", "Razer", "Yellow", Colors[26], "Linear", 45.0f, 1.2f, 3.5f); // Razer Yellow
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

    
    // Key pressed
    private void KeyPressed(int i) {
        Keys[i].AddInteractionPunch(mechanicalKeys[i].GetKeySwitch().GetForce() / 100.0f);
        PlayKeySound(mechanicalKeys[i].GetKeySwitch().GetSound());
        // Actuation - Travel Distance
        // Turns on screen light
        // Distinguishes tapping and holding
    }

    // Plays the key sound
    private void PlayKeySound(string sound) {
        if (sound == "Clicky") { }
            // Play clicky

        else if (sound == "Linear") { }
            // Play linear

        else if (sound == "Tactile") { }
            // Play tactile
    }
    

    // Sets the keys
    private void SetKeys() {
        char[] keyboardText = { 'X', 'B', 'C', 'R', 'L' };
        int[] numbers = new int[5];

        // Sets the switches
        for (int i = 0; i < numbers.Length; i++)
            numbers[i] = UnityEngine.Random.Range(0, switches.Length);

        keyOrder = AssignNumbers(numbers);

        // Assigns the data to each key and logs it
        for (int i = 0; i < mechanicalKeys.Length; i++) {
            mechanicalKeys[i] = new MechanicalKey(Keys[i], switches[numbers[i]], Screens[i], keyOrder[i], keyboardText[i]);
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


    // Gets the key hold order
    private void GetHoldOrder() {
        InitiateGrid();
        SetStartPos();
        MoveKeys();
    }

    // Moves the keys on Table 3
    private void MoveKeys() {
        for (iterations = 1; iterations <= 10; iterations++) {
            int ruleNo = 0;
            bool[] iterationRules = serialNumberConversions;
            bool lastRuleTrue = false;
            bool moved = false;
            int[] previousPos = new int[2];
            int[] newPos = new int[2];

            for (int i = 0; i < mechanicalKeys.Length; i++) {
                UpdateRules(mechanicalKeys[i].GetKeySwitch(), mechanicalKeys[i].GetNumber());
                lastRuleTrue = false;

                for (int j = 1; j <= mechanicalKeys.Length; j++) {

                    if (mechanicalKeys[i].GetNumber() == j) {

                        for (int k = 1; k <= j; k++) {
                            moved = false;

                            while (moved == false) {
                                previousPos = mechanicalKeys[i].GetGridPos();

                                /* Cannot go:
                                 * Up =     previousPos[0] - 1 < 0
                                 * Down =   previousPos[0] + 1 > 8
                                 * DLeft =  previousPos[1] - 1 < 0 || tableGridPos[previousPos[0] + 1][previousPos[1] - 1] != 0
                                 * DRight = previousPos[1] + 1 > 8 || tableGridPos[previousPos[0] + 1][previousPos[1] + 1] != 0
                                 * ULeft =  previousPos[1] - 1 < 0 || tableGridPos[previousPos[0] - 1][previousPos[1] - 1] != 0
                                 * URight = previousPos[1] + 1 > 8 || tableGridPos[previousPos[0] - 1][previousPos[1] + 1] != 0
                                 */

                                // If the key can't move at all
                                if ((previousPos[0] + 1 > 8 ||
                                    ((previousPos[1] - 1 < 0 || tableGridPos[previousPos[0] + 1][previousPos[1] - 1] != 0)
                                    && (previousPos[1] + 1 > 8 || tableGridPos[previousPos[0] + 1][previousPos[1] + 1] != 0)))
                                    && (previousPos[0] - 1 < 0 ||
                                    ((previousPos[1] - 1 < 0 || tableGridPos[previousPos[0] - 1][previousPos[1] - 1] != 0)
                                    && (previousPos[1] + 1 > 8 || tableGridPos[previousPos[0] - 1][previousPos[1] + 1] != 0)))) {

                                    moved = true;
                                }

                                // If the key can't move down and the last rule was true
                                else if ((previousPos[0] + 1 > 8 ||
                                    ((previousPos[1] - 1 < 0 || tableGridPos[previousPos[0] + 1][previousPos[1] - 1] != 0)
                                    && (previousPos[1] + 1 > 8 || tableGridPos[previousPos[0] + 1][previousPos[1] + 1] != 0)))
                                    && lastRuleTrue == true) {

                                    RotateGridClock();
                                }

                                // If the key can't move down
                                else if (previousPos[0] + 1 > 8 ||
                                    ((previousPos[1] - 1 < 0 || tableGridPos[previousPos[0] + 1][previousPos[1] - 1] != 0)
                                    && (previousPos[1] + 1 > 8 || tableGridPos[previousPos[0] + 1][previousPos[1] + 1] != 0))) {

                                    RotateGrid180();
                                }

                                // If the key can't move in the specified direction due to a nonexistant space
                                else if ((iterationRules[ruleNo] == false && previousPos[1] - 1 < 0) ||
                                    (iterationRules[ruleNo] == true && previousPos[1] + 1 > 8)) {

                                    if (iterationRules[ruleNo] == false)
                                        iterationRules[ruleNo] = true;

                                    else
                                        iterationRules[ruleNo] = false;
                                }

                                // If the key can't move in the specified direction due to an occupied space
                                else if ((iterationRules[ruleNo] == false && tableGridPos[previousPos[0] + 1][previousPos[1] - 1] != 0) ||
                                    (iterationRules[ruleNo] == true && tableGridPos[previousPos[0] + 1][previousPos[1] + 1] != 0)) {

                                    RotateGridCounter();
                                }


                                // Key moves
                                else {
                                    // Down-right
                                    if (iterationRules[ruleNo] == true) {
                                        newPos[0] = previousPos[0] + 1;
                                        newPos[1] = previousPos[1] + 1;
                                    }

                                    // Down-left
                                    else {
                                        newPos[0] = previousPos[0] + 1;
                                        newPos[1] = previousPos[1] - 1;
                                    }


                                    tableGridPos[newPos[0]][newPos[1]] = j;
                                    tableGridPos[previousPos[0]][previousPos[1]] = 0;
                                    moved = true;

                                    UpdateGridPos();

                                    // Checks rule for the space
                                    if (rules[tableGrid[newPos[0]][newPos[1]] - 1] == true) {
                                        lastRuleTrue = true;

                                        if (k == j)
                                            RotateGridCounter();

                                        else
                                            RotateGridClock();
                                    }

                                    else
                                        lastRuleTrue = false;
                                }
                            }

                            ruleNo++;
                        }
                    }
                }
            }

            LogKeyPositions();
        }

        // Sets grid to original rotation
        if (gridRotation == 1)
            RotateGridCounter();

        else if (gridRotation == 2)
            RotateGrid180();

        else if (gridRotation == 3)
            RotateGridClock();
    }

    // Sets the positions of the keys on the grid
    private void SetStartPos() {
        for (int i = 0; i < mechanicalKeys.Length; i++) {

            for (int j = 1; j <= mechanicalKeys.Length; j++) {

                if (mechanicalKeys[i].GetNumber() == j) {

                    for (int k = 0; k < keyOrder.Length; k++) {

                        if (keyOrder[k] == j) {

                            tableGridPos[0][k * 2] = j;
                            int[] newGridPos = { 0, k * 2 };
                            mechanicalKeys[i].SetGridPos(newGridPos);
                        }
                    }
                }
            }
        }

        LogKeyPositions();
    }

    // Gets the positions of each key in reading order
    private void GetOrderSequence() {
        int keysFound = 0;

        for (int i = 0; i < 9; i++) {

            for (int j = 0; j < 9; j++) {

                if (tableGridPos[i][j] != 0) {
                    holdOrder[keysFound] = tableGridPos[i][j];
                    keysFound++;
                }
            }
        }

        // Insert code that test if any unlit indicators match the switches' colors.

        Debug.LogFormat("[Mechanical Switches #{0}] The hold order selected is {1}, {2}, {3}, {4}, {5}", moduleId, holdOrder[0], holdOrder[1], holdOrder[2], holdOrder[3], holdOrder[4]);
    }


    // Logs the key's positions
    private void LogKeyPositions() {
        string iterationPlural = "";
        string rotationDirection = "";

        // Writes plural or singular form of 'iteration'
        if (iterations == 1)
            iterationPlural = "iteration";

        else
            iterationPlural = "iterations";

        // Changes the grid rotation into a cardinal direction
        rotationDirection = GetRotationCardinal();

        Debug.LogFormat("[Mechanical Switches #{0}] After {1} {2}, " +
            "Key {3} is in {4}{5}, " +
            "Key {6} is in {7}{8}, " +
            "Key {9} is in {10}{11}, " +
            "Key {12} is in {13}{14}, and " +
            "Key {15} is in {16}{17}. " +
            "Current rotation of Table 3: North is {}.",
            moduleId, iterations, iterationPlural,
            mechanicalKeys[0].GetNumber(), mechanicalKeys[0].GetGridPosLogger0(), mechanicalKeys[0].GetGridPosLogger1(),
            mechanicalKeys[1].GetNumber(), mechanicalKeys[1].GetGridPosLogger0(), mechanicalKeys[1].GetGridPosLogger1(),
            mechanicalKeys[2].GetNumber(), mechanicalKeys[2].GetGridPosLogger0(), mechanicalKeys[2].GetGridPosLogger1(),
            mechanicalKeys[3].GetNumber(), mechanicalKeys[3].GetGridPosLogger0(), mechanicalKeys[3].GetGridPosLogger1(),
            mechanicalKeys[4].GetNumber(), mechanicalKeys[4].GetGridPosLogger0(), mechanicalKeys[4].GetGridPosLogger1(),
            rotationDirection);
    }

    private string GetRotationCardinal() {
        switch (gridRotation) {
            case 0: return "North";
            case 1: return "East";
            case 2: return "South";
            case 3: return "West";
            default: return "North";
        }
    }

    // Updates keys' positions on the grid
    private void UpdateGridPos() {
        for (int i = 0; i < mechanicalKeys.Length; i++) {

            for (int j = 1; j <= mechanicalKeys.Length; j++) {

                if (mechanicalKeys[i].GetNumber() == j) {

                    for (int k = 0; k < 9; k++) {

                        for (int l = 0; l < 9; l++) {

                            if (tableGridPos[k][l] == j) {

                                int[] receivedGridNumbers = { k, l };
                                mechanicalKeys[i].SetGridPos(receivedGridNumbers);
                            }
                        }
                    }
                }
            }
        }
    }


    // Creates the grid
    private void InitiateGrid() {
        gridRotation = 0;

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

    // Rotates the grid clockwise 90 degrees
    private void RotateGridClock() {
        gridRotation = (gridRotation + 1) % 4;

        int[][] tempGrid = tableGrid;
        int[][] tempGridPos = tableGridPos;

        // Rule positions
        tableGrid[0][8] = tempGrid[0][0];
        tableGrid[2][8] = tempGrid[0][2];
        tableGrid[4][8] = tempGrid[0][4];
        tableGrid[6][8] = tempGrid[0][6];
        tableGrid[8][8] = tempGrid[0][8];
        tableGrid[8][6] = tempGrid[2][8];
        tableGrid[8][4] = tempGrid[4][8];
        tableGrid[8][2] = tempGrid[6][8];
        tableGrid[8][0] = tempGrid[8][8];
        tableGrid[6][0] = tempGrid[8][6];
        tableGrid[4][0] = tempGrid[8][4];
        tableGrid[2][0] = tempGrid[8][2];
        tableGrid[0][0] = tempGrid[8][0];
        tableGrid[0][2] = tempGrid[6][0];
        tableGrid[0][4] = tempGrid[4][0];
        tableGrid[0][6] = tempGrid[2][0];

        tableGrid[1][7] = tempGrid[1][1];
        tableGrid[3][7] = tempGrid[1][3];
        tableGrid[5][7] = tempGrid[1][5];
        tableGrid[7][7] = tempGrid[1][7];
        tableGrid[7][5] = tempGrid[3][7];
        tableGrid[7][3] = tempGrid[5][7];
        tableGrid[7][1] = tempGrid[7][7];
        tableGrid[5][1] = tempGrid[7][5];
        tableGrid[3][1] = tempGrid[7][3];
        tableGrid[1][1] = tempGrid[7][1];
        tableGrid[1][3] = tempGrid[5][1];
        tableGrid[1][5] = tempGrid[3][1];

        tableGrid[2][6] = tempGrid[2][2];
        tableGrid[4][6] = tempGrid[2][4];
        tableGrid[6][6] = tempGrid[2][6];
        tableGrid[6][4] = tempGrid[4][6];
        tableGrid[6][2] = tempGrid[6][6];
        tableGrid[4][2] = tempGrid[6][4];
        tableGrid[2][2] = tempGrid[6][2];
        tableGrid[2][4] = tempGrid[4][2];

        tableGrid[3][5] = tempGrid[3][3];
        tableGrid[5][5] = tempGrid[3][5];
        tableGrid[5][3] = tempGrid[5][5];
        tableGrid[3][3] = tempGrid[5][3];

        // Key positions
        tableGridPos[0][8] = tempGridPos[0][0];
        tableGridPos[2][8] = tempGridPos[0][2];
        tableGridPos[4][8] = tempGridPos[0][4];
        tableGridPos[6][8] = tempGridPos[0][6];
        tableGridPos[8][8] = tempGridPos[0][8];
        tableGridPos[8][6] = tempGridPos[2][8];
        tableGridPos[8][4] = tempGridPos[4][8];
        tableGridPos[8][2] = tempGridPos[6][8];
        tableGridPos[8][0] = tempGridPos[8][8];
        tableGridPos[6][0] = tempGridPos[8][6];
        tableGridPos[4][0] = tempGridPos[8][4];
        tableGridPos[2][0] = tempGridPos[8][2];
        tableGridPos[0][0] = tempGridPos[8][0];
        tableGridPos[0][2] = tempGridPos[6][0];
        tableGridPos[0][4] = tempGridPos[4][0];
        tableGridPos[0][6] = tempGridPos[2][0];

        tableGridPos[1][7] = tempGridPos[1][1];
        tableGridPos[3][7] = tempGridPos[1][3];
        tableGridPos[5][7] = tempGridPos[1][5];
        tableGridPos[7][7] = tempGridPos[1][7];
        tableGridPos[7][5] = tempGridPos[3][7];
        tableGridPos[7][3] = tempGridPos[5][7];
        tableGridPos[7][1] = tempGridPos[7][7];
        tableGridPos[5][1] = tempGridPos[7][5];
        tableGridPos[3][1] = tempGridPos[7][3];
        tableGridPos[1][1] = tempGridPos[7][1];
        tableGridPos[1][3] = tempGridPos[5][1];
        tableGridPos[1][5] = tempGridPos[3][1];

        tableGridPos[2][6] = tempGridPos[2][2];
        tableGridPos[4][6] = tempGridPos[2][4];
        tableGridPos[6][6] = tempGridPos[2][6];
        tableGridPos[6][4] = tempGridPos[4][6];
        tableGridPos[6][2] = tempGridPos[6][6];
        tableGridPos[4][2] = tempGridPos[6][4];
        tableGridPos[2][2] = tempGridPos[6][2];
        tableGridPos[2][4] = tempGridPos[4][2];

        tableGridPos[3][5] = tempGridPos[3][3];
        tableGridPos[5][5] = tempGridPos[3][5];
        tableGridPos[5][3] = tempGridPos[5][5];
        tableGridPos[3][3] = tempGridPos[5][3];

        UpdateGridPos();
    }

    // Rotates the grid counter-clockwise 90 degrees
    private void RotateGridCounter() {
        gridRotation = (gridRotation + 3) % 4;

        int[][] tempGrid = tableGrid;
        int[][] tempGridPos = tableGridPos;

        // Rule positions
        tableGrid[0][0] = tempGrid[0][8];
        tableGrid[0][2] = tempGrid[2][8];
        tableGrid[0][4] = tempGrid[4][8];
        tableGrid[0][6] = tempGrid[6][8];
        tableGrid[0][8] = tempGrid[8][8];
        tableGrid[2][8] = tempGrid[8][6];
        tableGrid[4][8] = tempGrid[8][4];
        tableGrid[6][8] = tempGrid[8][2];
        tableGrid[8][8] = tempGrid[8][0];
        tableGrid[8][6] = tempGrid[6][0];
        tableGrid[8][4] = tempGrid[4][0];
        tableGrid[8][2] = tempGrid[2][0];
        tableGrid[8][0] = tempGrid[0][0];
        tableGrid[6][0] = tempGrid[0][2];
        tableGrid[4][0] = tempGrid[0][4];
        tableGrid[2][0] = tempGrid[0][6];

        tableGrid[1][1] = tempGrid[1][7];
        tableGrid[1][3] = tempGrid[3][7];
        tableGrid[1][5] = tempGrid[5][7];
        tableGrid[1][7] = tempGrid[7][7];
        tableGrid[3][7] = tempGrid[7][5];
        tableGrid[5][7] = tempGrid[7][3];
        tableGrid[7][7] = tempGrid[7][1];
        tableGrid[7][5] = tempGrid[5][1];
        tableGrid[7][3] = tempGrid[3][1];
        tableGrid[7][1] = tempGrid[1][1];
        tableGrid[5][1] = tempGrid[1][3];
        tableGrid[3][1] = tempGrid[1][5];

        tableGrid[2][2] = tempGrid[2][6];
        tableGrid[2][4] = tempGrid[4][6];
        tableGrid[2][6] = tempGrid[6][6];
        tableGrid[4][6] = tempGrid[6][4];
        tableGrid[6][6] = tempGrid[6][2];
        tableGrid[6][4] = tempGrid[4][2];
        tableGrid[6][2] = tempGrid[2][2];
        tableGrid[4][2] = tempGrid[2][4];

        tableGrid[3][3] = tempGrid[3][5];
        tableGrid[3][5] = tempGrid[5][5];
        tableGrid[5][5] = tempGrid[5][3];
        tableGrid[5][3] = tempGrid[3][3];

        // Key positions
        tableGridPos[0][0] = tempGridPos[0][8];
        tableGridPos[0][2] = tempGridPos[2][8];
        tableGridPos[0][4] = tempGridPos[4][8];
        tableGridPos[0][6] = tempGridPos[6][8];
        tableGridPos[0][8] = tempGridPos[8][8];
        tableGridPos[2][8] = tempGridPos[8][6];
        tableGridPos[4][8] = tempGridPos[8][4];
        tableGridPos[6][8] = tempGridPos[8][2];
        tableGridPos[8][8] = tempGridPos[8][0];
        tableGridPos[8][6] = tempGridPos[6][0];
        tableGridPos[8][4] = tempGridPos[4][0];
        tableGridPos[8][2] = tempGridPos[2][0];
        tableGridPos[8][0] = tempGridPos[0][0];
        tableGridPos[6][0] = tempGridPos[0][2];
        tableGridPos[4][0] = tempGridPos[0][4];
        tableGridPos[2][0] = tempGridPos[0][6];

        tableGridPos[1][1] = tempGridPos[1][7];
        tableGridPos[1][3] = tempGridPos[3][7];
        tableGridPos[1][5] = tempGridPos[5][7];
        tableGridPos[1][7] = tempGridPos[7][7];
        tableGridPos[3][7] = tempGridPos[7][5];
        tableGridPos[5][7] = tempGridPos[7][3];
        tableGridPos[7][7] = tempGridPos[7][1];
        tableGridPos[7][5] = tempGridPos[5][1];
        tableGridPos[7][3] = tempGridPos[3][1];
        tableGridPos[7][1] = tempGridPos[1][1];
        tableGridPos[5][1] = tempGridPos[1][3];
        tableGridPos[3][1] = tempGridPos[1][5];

        tableGridPos[2][2] = tempGridPos[2][6];
        tableGridPos[2][4] = tempGridPos[4][6];
        tableGridPos[2][6] = tempGridPos[6][6];
        tableGridPos[4][6] = tempGridPos[6][4];
        tableGridPos[6][6] = tempGridPos[6][2];
        tableGridPos[6][4] = tempGridPos[4][2];
        tableGridPos[6][2] = tempGridPos[2][2];
        tableGridPos[4][2] = tempGridPos[2][4];

        tableGridPos[3][3] = tempGridPos[3][5];
        tableGridPos[3][5] = tempGridPos[5][5];
        tableGridPos[5][5] = tempGridPos[5][3];
        tableGridPos[5][3] = tempGridPos[3][3];

        UpdateGridPos();
    }

    // Rotates the grid 180 degrees
    private void RotateGrid180() {
        gridRotation = (gridRotation + 2) % 4;

        int[][] tempGrid = tableGrid;
        int[][] tempGridPos = tableGridPos;

        // Rule positions
        tableGrid[0][8] = tempGrid[8][0];
        tableGrid[2][8] = tempGrid[6][0];
        tableGrid[4][8] = tempGrid[4][0];
        tableGrid[6][8] = tempGrid[2][0];
        tableGrid[8][8] = tempGrid[0][0];
        tableGrid[8][6] = tempGrid[0][2];
        tableGrid[8][4] = tempGrid[0][4];
        tableGrid[8][2] = tempGrid[0][6];
        tableGrid[8][0] = tempGrid[0][8];
        tableGrid[6][0] = tempGrid[2][8];
        tableGrid[4][0] = tempGrid[4][8];
        tableGrid[2][0] = tempGrid[6][8];
        tableGrid[0][0] = tempGrid[8][8];
        tableGrid[0][2] = tempGrid[8][6];
        tableGrid[0][4] = tempGrid[8][4];
        tableGrid[0][6] = tempGrid[8][2];

        tableGrid[1][7] = tempGrid[7][1];
        tableGrid[3][7] = tempGrid[5][1];
        tableGrid[5][7] = tempGrid[3][1];
        tableGrid[7][7] = tempGrid[1][1];
        tableGrid[7][5] = tempGrid[1][3];
        tableGrid[7][3] = tempGrid[1][5];
        tableGrid[7][1] = tempGrid[1][7];
        tableGrid[5][1] = tempGrid[3][7];
        tableGrid[3][1] = tempGrid[5][7];
        tableGrid[1][1] = tempGrid[7][7];
        tableGrid[1][3] = tempGrid[7][5];
        tableGrid[1][5] = tempGrid[7][3];

        tableGrid[2][6] = tempGrid[6][2];
        tableGrid[4][6] = tempGrid[6][4];
        tableGrid[6][6] = tempGrid[2][2];
        tableGrid[6][4] = tempGrid[2][4];
        tableGrid[6][2] = tempGrid[2][6];
        tableGrid[4][2] = tempGrid[4][6];
        tableGrid[2][2] = tempGrid[6][6];
        tableGrid[2][4] = tempGrid[6][4];

        tableGrid[3][5] = tempGrid[5][3];
        tableGrid[5][5] = tempGrid[3][3];
        tableGrid[5][3] = tempGrid[3][5];
        tableGrid[3][3] = tempGrid[5][5];

        // Key positions
        tableGridPos[0][8] = tempGridPos[8][0];
        tableGridPos[2][8] = tempGridPos[6][0];
        tableGridPos[4][8] = tempGridPos[4][0];
        tableGridPos[6][8] = tempGridPos[2][0];
        tableGridPos[8][8] = tempGridPos[0][0];
        tableGridPos[8][6] = tempGridPos[0][2];
        tableGridPos[8][4] = tempGridPos[0][4];
        tableGridPos[8][2] = tempGridPos[0][6];
        tableGridPos[8][0] = tempGridPos[0][8];
        tableGridPos[6][0] = tempGridPos[2][8];
        tableGridPos[4][0] = tempGridPos[4][8];
        tableGridPos[2][0] = tempGridPos[6][8];
        tableGridPos[0][0] = tempGridPos[8][8];
        tableGridPos[0][2] = tempGridPos[8][6];
        tableGridPos[0][4] = tempGridPos[8][4];
        tableGridPos[0][6] = tempGridPos[8][2];

        tableGridPos[1][7] = tempGridPos[7][1];
        tableGridPos[3][7] = tempGridPos[5][1];
        tableGridPos[5][7] = tempGridPos[3][1];
        tableGridPos[7][7] = tempGridPos[1][1];
        tableGridPos[7][5] = tempGridPos[1][3];
        tableGridPos[7][3] = tempGridPos[1][5];
        tableGridPos[7][1] = tempGridPos[1][7];
        tableGridPos[5][1] = tempGridPos[3][7];
        tableGridPos[3][1] = tempGridPos[5][7];
        tableGridPos[1][1] = tempGridPos[7][7];
        tableGridPos[1][3] = tempGridPos[7][5];
        tableGridPos[1][5] = tempGridPos[7][3];

        tableGridPos[2][6] = tempGridPos[6][2];
        tableGridPos[4][6] = tempGridPos[6][4];
        tableGridPos[6][6] = tempGridPos[2][2];
        tableGridPos[6][4] = tempGridPos[2][4];
        tableGridPos[6][2] = tempGridPos[2][6];
        tableGridPos[4][2] = tempGridPos[4][6];
        tableGridPos[2][2] = tempGridPos[6][6];
        tableGridPos[2][4] = tempGridPos[6][4];

        tableGridPos[3][5] = tempGridPos[5][3];
        tableGridPos[5][5] = tempGridPos[3][3];
        tableGridPos[5][3] = tempGridPos[3][5];
        tableGridPos[3][3] = tempGridPos[5][5];

        UpdateGridPos();
    }
}