namespace ClippyAI.Models;

public class Embedding
{
    public long Id { get; set; }
    public string Answer { get; set; } = "";
    public string AnswerVector { get; set; } = "";
    public float Distance { get; set; } = 0.0f;
}
