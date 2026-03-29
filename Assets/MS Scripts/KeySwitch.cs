using UnityEngine;

public class KeySwitch {
    private int id;
    private int column;

    private string name;
    private string brand;
    private Color color;
    private string colorName;

    private string category;
    private float[] force;
    private float actuation;
    private float travelDistance;

    public KeySwitch(int id, int column, string name, string brand, Color color, string colorName, string category, float[] force, float actuation, float travelDistance) {
        this.id = id;
        this.column = column;

        this.name = name;
        this.brand = brand;
        this.color = color;
        this.colorName = colorName;

        this.category = category;
        this.force = force;
        this.actuation = actuation;
        this.travelDistance = travelDistance;
    }

    public int GetId() {
        return id;
    }

    public int GetColumn() {
        return column;
    }

    public string GetName() {
        return name;
    }

    public string GetBrand() {
        return brand;
    }

    public Color GetColor() {
        return color;
    }

    public string GetColorName() {
        return colorName;
    }

    public string GetCategory() {
        return category;
    }

    public float[] GetForce() {
        return force;
    }

    public float GetActuation() {
        return actuation;
    }

    public float GetTravelDistance() {
        return travelDistance;
    }
}