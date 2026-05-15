using UnityEngine;

#if LLMUNITY_AVAILABLE
using LLMUnity;
#endif

namespace AIRA.AI
{
    public class LLMGuard : MonoBehaviour
    {
        // Guard singleton LLM component
        private void Awake()
        {
#if LLMUNITY_AVAILABLE
            var allLLMs = FindObjectsByType<LLM>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var llm in allLLMs)
            {
                if (llm.gameObject != gameObject && llm.gameObject.scene.buildIndex == -1)
                {
                    Destroy(gameObject);
                    return;
                }
            }
#endif
            DontDestroyOnLoad(gameObject);
        }
    }
}
