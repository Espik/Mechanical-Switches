﻿using System;
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
    private int condition;
    private bool pressSafe = true;
    private int pressTime = -1;
    private bool doublePressTime = false;
    private bool needRapidTapping = false;
    private char text;

    private int rotation = 0;
    private string rotationLogger = "North";
    /* North = 0
     * East = 1
     * South = 2
     * West = 3
     */

    private int[] gridPos = { 0, 0 };
    private string[] gridPosLogger = new string[2];

    // Default Setup
    public MechanicalKey(KMSelectable key, KeySwitch keySwitch, Renderer screen, int condition, int number, char text) {
        this.key = key;
        this.keySwitch = keySwitch;
        this.screen = screen;
        this.condition = condition;
        this.number = number;
        this.text = text;

        pressSafe = true;
        pressTime = -1;
        doublePressTime = false;
        needRapidTapping = false;

        this.screen.material = this.keySwitch.GetMaterial();

        if (this.keySwitch.GetClear() == true) {
            // Set a clear material to the key
        }
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

    public void SetCondition(int condition) {
        this.condition = condition;
    }

    public void SetNumber(int number) {
        this.number = number;
    }

    public void SetPressSafe(bool pressSafe) {
        this.pressSafe = pressSafe;
    }

    public void SetPressTime(int pressTime) {
        this.pressTime = pressTime;
    }

    public void SetDoublePressTime(bool doublePressTime) {
        this.doublePressTime = doublePressTime;
    }

    public void SetNeedRapidTapping(bool needRapidTapping) {
        this.needRapidTapping = needRapidTapping;
    }

    public void SetText(char text) {
        this.text = text;
    }

    public void SetRotation(int rotation) {
        this.rotation = rotation;
        rotationLogger = SetGridPosLogger(this.rotation);
    }

    public void SetGridPos(int[] gridPos) {
        this.gridPos = gridPos;

        gridPosLogger[0] = SetGridPosLogger(this.gridPos[1]);
        gridPosLogger[1] = (this.gridPos[0] + 1).ToString();
    }

    // Sets the rotation for logging
    public string SetRotationLogger(int rotation) {
        switch (rotation) {
            case 0: return "North";
            case 1: return "East";
            case 2: return "South";
            case 3: return "West";
            default: return "North";
        }
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

    public void SetMaterial(Material material) {
        screen.material = material;
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

    public int GetCondition() {
        return condition;
    }

    public int GetNumber() {
        return number;
    }

    public bool GetPressSafe() {
        return pressSafe;
    }

    public int GetPressTime() {
        return pressTime;
    }

    public bool GetDoublePressTime() {
        return doublePressTime;
    }

    public bool GetNeedRapidTapping() {
        return needRapidTapping;
    }

    public char GetText() {
        return text;
    }

    public int GetRotation() {
        return rotation;
    }

    public string GetRotationLogger() {
        return rotationLogger;
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
