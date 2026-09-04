namespace Application.DTOs.Ai;

/// <summary>Input: the requestor's free-text description ("a box of A4 paper and 2 black pens by Friday").</summary>
public sealed record RequestAssistantCommand(string Text);
