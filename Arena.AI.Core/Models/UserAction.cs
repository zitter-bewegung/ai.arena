namespace Arena.AI.Core.Models;

public class UserAction
{
    public UserActionType ActionType => Destination is null ? Target is null ? UserActionType.Skip : UserActionType.Attack : UserActionType.Move;
    public string? Destination { get; set; }
    public string? Target { get; set; }
    public string? Label { get; set; }

    public static UserAction Skip(string? label = null) => new() { Destination = null, Target = null, Label = label };
    public static UserAction Move(string destination, string? label = null) => new() { Destination = destination, Target = null, Label = label };
    public static UserAction Attack(string target, string? label = null) => new() { Destination = null, Target = target, Label = label };
}
