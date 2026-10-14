using System.Collections.Generic;
using UnityEngine;

namespace Yang.Dialogue
{
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
        public List<string> viewIDs = new();
    }

    [System.Serializable]
    public class ViewDataEntry
    {
        public string key;
        public string viewID;
        public string data;
    }
}
