using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KModkit;

public class KeySwitch {
    // Properties
    private string name;
    private string brand;
    private string color;
    private Material material;

    private string sound;
    private float force;
    private float actuation;
    private float travelDistance;

    private int column;

    // Default setup
    public KeySwitch(string name, string brand, string color, Material material, string sound, float force, float actuation, float travelDistance, int column) {
        this.name = name;
        this.brand = brand;
        this.color = color;
        this.material = material;
        this.sound = sound;
        this.force = force;
        this.actuation = actuation;
        this.travelDistance = travelDistance;
        this.column = column;
    }

    
    // Sets info
    public void SetName(string name) {
        this.name = name;
    }

    public void SetBrand(string brand) {
        this.brand = brand;
    }

    public void SetColor(string color) {
        this.color = color;
    }

    public void SetMaterial(Material material) {
        this.material = material;
    }

    public void SetSound(string sound) {
        this.sound = sound;
    }

    public void SetForce(float force) {
        this.force = force;
    }

    public void SetActuation(float actuation) {
        this.actuation = actuation;
    }

    public void SetTravelDistance(float travelDistance) {
        this.travelDistance = travelDistance;
    }

    public void SetColumn(int column) {
        this.column = column;
    }


    // Gets info
    public string GetName() {
        return name;
    }

    public string GetBrand() {
        return brand;
    }

    public string GetColor() {
        return color;
    }

    public Material GetMaterial() {
        return material;
    }

    public string GetSound() {
        return sound;
    }

    public float GetForce() {
        return force;
    }

    public float GetActuation() {
        return actuation;
    }

    public float GetTravelDistance() {
        return travelDistance;
    }

    public int GetColumn() {
        return column;
    }
}