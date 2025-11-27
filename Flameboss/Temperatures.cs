namespace Flameboss;

public record Temperatures(
    string Pit = "-1", 
    string Meat1 = "-1", 
    string SetTemp = "-1",
    string Blower = "0%",
    DateTime LastUpdate = default
    );