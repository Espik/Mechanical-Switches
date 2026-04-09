public class MechanicalKey {
    private int modulePosition;
    private KeySwitch keySwitch;
    private int number;

    private int[] tablePosition;
    private int rotation;

    public MechanicalKey(int modulePosition, KeySwitch keySwitch) {
        this.modulePosition = modulePosition;
        this.keySwitch = keySwitch;

        number = modulePosition;

        tablePosition = new[] { 0, 2 * number };
        rotation = 0;
    }

    public void SetNumber(int number) {
        this.number = number;
    }

    public void SetTablePosition(int[] tablePosition) {
        this.tablePosition = tablePosition;
    }

    public void SetRotation(int rotation) {
        this.rotation = rotation;
    }

    public int GetModulePosition() {
        return modulePosition;
    }

    public KeySwitch GetKeySwitch() {
        return keySwitch;
    }

    public int GetNumber() {
        return number;
    }

    public int[] GetTablePosition() {
        return tablePosition;
    }

    public int GetRotation() {
        return rotation;
    }
}
