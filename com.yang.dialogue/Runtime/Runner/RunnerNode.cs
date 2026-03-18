using System.Collections.Generic;
using System.Threading.Tasks;

namespace Yang.Dialogue
{
    public class RunnerNode
    {
        public event System.Action<string, System.Action> StopCallback;

        private readonly List<RunnerText> runnerDatas = new();
        private readonly List<UnityEngine.Object> runnerObjects = new();

        private readonly RunnerEvent runnerEvent;
        private readonly RunnerTrigger runnerTrigger;

        private readonly Dictionary<string, NodeData> nodes = new();
        private readonly Dictionary<RunnerPort, RunnerPort> links = new();

        public RunnerNode(RunnerEvent runnerEvent, RunnerTrigger runnerTrigger)
        {
            this.runnerEvent = runnerEvent;
            this.runnerTrigger = runnerTrigger;
        }

        public void SetDatas(DialogueSO so)
        {
            if (so == null) return;

            so.GetDatas(nodes, links);
        }

        public async Task<bool> NextNode(IReadOnlyList<IDialogueView> views, RunnerToken token)
        {
            NodeData nodeData = nodes[token.targetNode];

            switch (nodeData.type)
            {
                case NodeType.Start:
                    if (CheckProceed(token, 0)) return true;
                    break;

                case NodeType.Dialogue:
                    {
                        token.PointNode = token.targetNode;

                        IReadOnlyList<GenericData> speakerTable = nodeData.OptionDatas[0].data;
                        IReadOnlyList<GenericData> speakerEntry = nodeData.OptionDatas[1].data;

                        IReadOnlyList<GenericData> textTable = nodeData.OptionDatas[2].data;
                        IReadOnlyList<GenericData> textEntry = nodeData.OptionDatas[3].data;

                        IReadOnlyList<GenericData> message = nodeData.OptionDatas[4].data;

                        RunnerText speaker = new(speakerTable[0].ToString(), speakerEntry[0].ToString());
                        RunnerText text = new(textTable[0].ToString(), textEntry[0].ToString());

                        foreach (IDialogueView view in views) await view.OnDialogue(speaker, text, message[0].ToString(), token);

                        if (CheckProceed(token, 0)) return true;
                    }
                    break;

                case NodeType.Condition:
                    {
                        IReadOnlyList<DataWrapper> portDatas = nodeData.PortDatas;

                        bool found = false;

                        for (int i = 1; i < portDatas.Count; i++)
                        {
                            bool allExist = true;
                            IReadOnlyList<GenericData> datas = portDatas[i].data;

                            for (int j = 0; j < datas.Count; j++)
                            {
                                if (!runnerTrigger.IsTrigger(datas[j].ToString()))
                                {
                                    allExist = false;

                                    break;
                                }
                            }

                            if (allExist && CheckProceed(token, i)) return true;
                        }

                        if (!found && CheckProceed(token, 0)) return true;
                    }
                    break;

                case NodeType.Trigger:
                    {
                        IReadOnlyList<DataWrapper> optionDatas = nodeData.OptionDatas;

                        for (int i = 0; i < optionDatas.Count; i++)
                        {
                            IReadOnlyList<GenericData> datas = optionDatas[i].data;

                            string value = datas[0].ToString();

                            if (value == "") continue;

                            if (datas[1].TryGetBool(out bool isTrigger))
                            {
                                if (isTrigger) runnerTrigger.SetTrigger(value);
                                else runnerTrigger.UnsetTrigger(value);
                            }
                        }

                        if (CheckProceed(token, 0)) return true;
                    }
                    break;

                case NodeType.Event:
                    {
                        IReadOnlyList<DataWrapper> optionDatas = nodeData.OptionDatas;

                        for (int i = 0; i < optionDatas.Count; i++)
                        {
                            IReadOnlyList<GenericData> datas = optionDatas[i].data;

                            string value = datas[0].ToString();

                            if (value == "") continue;

                            runnerEvent.OnEvent(value);
                        }

                        if (CheckProceed(token, 0)) return true;
                    }
                    break;

                case NodeType.Choice:
                    {
                        token.PointNode = token.targetNode;

                        runnerDatas.Clear();

                        IReadOnlyList<DataWrapper> textEntries = nodeData.PortDatas;

                        IReadOnlyList<GenericData> speakerTable = nodeData.OptionDatas[0].data;
                        IReadOnlyList<GenericData> speakerEntry = nodeData.OptionDatas[1].data;

                        IReadOnlyList<GenericData> textTable = nodeData.OptionDatas[2].data;

                        IReadOnlyList<GenericData> message = nodeData.OptionDatas[3].data;

                        RunnerText speaker = new(speakerTable[0].ToString(), speakerEntry[0].ToString());

                        for (int i = 0; i < textEntries.Count; i++)
                        {
                            RunnerText data = new(i, textTable[0].ToString(), textEntries[i].data[0].ToString());

                            runnerDatas.Add(data);
                        }

                        foreach (IDialogueView view in views)
                        {
                            int result = await view.OnChoice(speaker, runnerDatas, message[0].ToString(), token);

                            if (result != -1)
                            {
                                if (CheckProceed(token, runnerDatas[result].portIndex)) return true;

                                break;
                            }
                        }
                    }
                    break;

                case NodeType.Wait:
                    {
                        token.PointNode = token.targetNode;

                        IReadOnlyList<GenericData> datas = nodeData.OptionDatas[0].data;

                        if (datas[0].TryGetEnum(out WaitType type))
                        {
                            switch (type)
                            {
                                case WaitType.Notify:
                                    bool isWait = true;

                                    foreach (IDialogueView view in views) StopCallback?.Invoke(datas[1].ToString(), () => isWait = false);

                                    runnerEvent.OnEvent(datas[1].ToString());

                                    while (isWait && !token.IsStop) await Task.Yield();
                                    break;

                                case WaitType.Seconds:
                                    if (datas[1].TryGetFloat(out float second)) await token.Delay(second);
                                    break;
                            }
                        }

                        if (CheckProceed(token, 0)) return true;
                    }
                    break;

                case NodeType.Object:
                    {
                        token.PointNode = token.targetNode;

                        runnerObjects.Clear();

                        IReadOnlyList<DataWrapper> optionDatas = nodeData.OptionDatas;

                        for (int i = 0; i < optionDatas.Count; i++)
                        {
                            IReadOnlyList<GenericData> datas = optionDatas[i].data;

                            if (datas[0].TryGetObject(out UnityEngine.Object value)) runnerObjects.Add(value);
                        }

                        foreach (IDialogueView view in views) await view.OnObject(runnerObjects, token);

                        if (CheckProceed(token, 0)) return true;
                    }
                    break;
            }

            if (token.IsStop) StopCallback?.Invoke("", null);

            return false;
        }

        public bool CheckNode(string nodeName)
        {
            if (nodeName == "" && !nodes.ContainsKey(nodeName)) return false;

            return true;
        }

        private bool CheckProceed(RunnerToken token, int portIndex)
        {
            if (token.IsStop) return false;

            if (CheckNode(token.targetNode)) return true;
            else
            {
                RunnerPort port = new(token.targetNode, portIndex);

                if (links.TryGetValue(port, out RunnerPort targetPort))
                {
                    token.targetNode = targetPort.guid;

                    return true;
                }
            }

            token.targetNode = token.PointNode;

            return false;
        }
    }
}