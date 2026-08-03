using System.Collections.Generic;
using UnityEngine;

namespace Yang.Dialogue
{
    /// <summary>
    /// Serializable save/load payload capturing a dialogue runner's in-progress state (task keys, their point nodes, and runtime values) for persistence and restore.
    /// </summary>
    [System.Serializable]
    public class DialogueSaveData
    {
        public List<DialogueFlowData> dialogueFlows = new();
        public List<RunnerValue> triggerValues = new();

        public List<ViewDataEntry> viewDatas = new();
    }

    [System.Serializable]
    public class DialogueFlowData
    {
        public string key;
        public string nodeGuid;
    }

    [System.Serializable]
    public class ViewDataEntry
    {
        public string viewID;
        public string data;
    }
}
