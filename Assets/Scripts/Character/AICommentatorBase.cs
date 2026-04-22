using System.Collections.Generic;
using System.Threading.Tasks;

namespace AIRA.Character
{
    // Abstract base komentar AI
    public abstract class AICommentatorBase
    {
        public enum EventPriority { LOW, NORMAL, HIGH }

        public struct GameEvent
        {
            public string                    eventType;
            public Dictionary<string, string> context;
            public EventPriority             priority;
        }

        protected Queue<GameEvent> _commentQueue  = new();
        protected bool             _isProcessing  = false;
        protected float            _lastCommentTime;

        // Masukkan event ke queue
        public void EnqueueComment(string eventType,
            Dictionary<string, string> context,
            EventPriority priority = EventPriority.NORMAL)
        {
            if (priority == EventPriority.LOW && _commentQueue.Count > 0)
                return;

            if (priority == EventPriority.HIGH)
                CollapseHighPriorityEvents();

            _commentQueue.Enqueue(new GameEvent
            {
                eventType = eventType,
                context   = context,
                priority  = priority
            });

            TryProcessNext();
        }

        // Proses event berikutnya
        protected async void TryProcessNext()
        {
            if (_isProcessing || _commentQueue.Count == 0) return;
            _isProcessing = true;
            var ev = _commentQueue.Dequeue();
            await ProcessNextComment(ev);
            _isProcessing = false;
        }

        // Subclass implementasi cara proses
        protected abstract Task ProcessNextComment(GameEvent gameEvent);

        // Pertahankan satu HIGH priority terbaru
        protected virtual void CollapseHighPriorityEvents()
        {
            var temp = new List<GameEvent>(_commentQueue);
            int highCount = 0;
            foreach (var e in temp)
                if (e.priority == EventPriority.HIGH) highCount++;

            if (highCount < 2) return;

            int lastHighIndex = -1;
            for (int i = temp.Count - 1; i >= 0; i--)
                if (temp[i].priority == EventPriority.HIGH) { lastHighIndex = i; break; }

            _commentQueue.Clear();
            for (int i = 0; i < temp.Count; i++)
                if (temp[i].priority != EventPriority.HIGH || i == lastHighIndex)
                    _commentQueue.Enqueue(temp[i]);
        }
    }
}
