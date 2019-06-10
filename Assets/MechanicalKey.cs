using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KModkit;

public class MechanicalKey {
    // Properties
    private KMSelectable key;
    private KeySwitch keySwitch;
    private Renderer screen;
    private int number;
    private char text;

    private int[] gridPos = { 0, 0 };
    private string[] gridPosLogger = new string[2];

    // Default Setup
    public MechanicalKey(KMSelectable key, KeySwitch keySwitch, Renderer screen, int number, char text) {
        this.key = key;
        this.keySwitch = keySwitch;
        this.screen = screen;
        this.number = number;
        this.text = text;

        this.screen.material = this.keySwitch.GetMaterial();
    }


    // Sets info
    public void SetKey(KMSelectable key) {
        this.key = key;
    }

    public void SetKeySwitch(KeySwitch keySwitch) {
        this.keySwitch = keySwitch;
    }

    public void SetScreen(Renderer screen) {
        this.screen = screen;
    }

    public void SetNumber(int number) {
        this.number = number;
    }

    public void SetText(char text) {
        this.text = text;
    }

    public void SetGridPos(int[] gridPos) {
        this.gridPos = gridPos;

        gridPosLogger[0] = SetGridPosLogger(gridPos[1]);
        gridPosLogger[1] = (gridPos[0] + 1).ToString();
    }

    // Sets the grid position for logging
    public string SetGridPosLogger(int pos) {
        switch(pos) {
            case 0: return "A";
            case 1: return "B";
            case 2: return "C";
            case 3: return "D";
            case 4: return "E";
            case 5: return "F";
            case 6: return "G";
            case 7: return "H";
            case 8: return "I";
            default: return "A";
        }
    }


    // Gets info
    public KMSelectable GetKey() {
        return key;
    }

    public KeySwitch GetKeySwitch() {
        return keySwitch;
    }

    public Renderer GetScreen() {
        return screen;
    }

    public int GetNumber() {
        return number;
    }

    public char GetText() {
        return text;
    }

    public int[] GetGridPos() {
        return gridPos;
    }

    public string GetGridPosLogger0() {
        return gridPosLogger[0];
    }

    public string GetGridPosLogger1() {
        return gridPosLogger[1];
    }
}
