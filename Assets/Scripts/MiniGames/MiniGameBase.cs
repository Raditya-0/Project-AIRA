using System;
using UnityEngine;

public abstract class MiniGameBase : MonoBehaviour
{
    // Events
    public event Action<int> OnGameEnd;

    // Properties
    public int CurrentScore { get; protected set; }
    public bool IsRunning { get; protected set; }

    // Abstract Interface
    public abstract void StartGame();
    public abstract void EndGame();
    public abstract void OnCorrectAnswer();
    public abstract void OnWrongAnswer();

    // Protected Helpers
    protected void TriggerAIComment(
        string eventType,
        string details,
        AICommentator.EventPriority priority = AICommentator.EventPriority.NORMAL)
    {
        if (AICommentator.Instance == null)
        {
            Debug.LogWarning("[MiniGameBase] AICommentator.Instance is null — skipping comment.");
            return;
        }

        var evt = new AICommentator.GameEvent(
            gameName:  GetType().Name,
            eventType: eventType,
            details:   details,
            score:     CurrentScore,
            priority:  priority
        );
        AICommentator.Instance.TriggerComment(evt);
    }

    protected void FinishGame()
    {
        IsRunning = false;
        Debug.Log($"[{GetType().Name}] Game finished. Final score: {CurrentScore}");
        OnGameEnd?.Invoke(CurrentScore);
        GameManager.Instance?.ChangeState(GameManager.GameState.IDLE);
    }
}
