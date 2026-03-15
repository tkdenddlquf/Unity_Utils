using System.Collections.Generic;
using System.Threading.Tasks;

namespace Yang.Dialogue
{
    public class RunnerNode
    {
        public string CurrentNode { get; private set; }

        public event System.Action EndCallback;
        public event System.Action<string, System.Action> StopCallback;

        private readonly List<RunnerText> runnerDatas = new();
        private readonly List<UnityEngine.Object> runnerObjects = new();

        private readonly DialogueRunner runner;
        private readonly RunnerEvent runnerEvent;

        private readonly Dictionary<string, NodeData> nodes = new();
        private readonly Dictionary<RunnerPort, RunnerPort> links = new();

        public RunnerNode(DialogueRunner runner, RunnerEvent runnerEvent)
        {
            this.runner = runner;
            this.runnerEvent = runnerEvent;
        }

        public void SetDatas(DialogueSO so, string node = "")
        {
            if (so == null) return;

            so.GetDatas(nodes, links);

            if (!JumpNode(node)) CurrentNode = so.StartGuid;
        }

        public async Task<string> NextNode(string currentNode, IRunnerToken token)
        {
            NodeData nodeData = nodes[currentNode];

            switch (nodeData.type)
            {
                case NodeType.Start:
                    {
                        RunnerPort port = new(currentNode, 0);

                        if (links.TryGetValue(port, out RunnerPort target)) return target.guid;
                    }
                    break;

                case NodeType.Dialogue:
                    {
                        CurrentNode = currentNode;

                        IReadOnlyList<GenericData> speakerTable = nodeData.optionDatas[0].data;
                        IReadOnlyList<GenericData> speakerEntry = nodeData.optionDatas[1].data;

                        IReadOnlyList<GenericData> textTable = nodeData.optionDatas[2].data;
                        IReadOnlyList<GenericData> textEntry = nodeData.optionDatas[3].data;

                        IReadOnlyList<GenericData> message = nodeData.optionDatas[4].data;

                        RunnerText speaker = new(speakerTable[0].ToString(), speakerEntry[0].ToString());
                        RunnerText text = new(textTable[0].ToString(), textEntry[0].ToString());

                        foreach (IDialogueView view in runner.Views) await view.OnDialogue(speaker, text, message[0].ToString(), token);

                        RunnerPort port = new(currentNode, 0);

                        if (links.TryGetValue(port, out RunnerPort target))
                        {
                            CurrentNode = target.guid;

                            return CurrentNode;
                        }
                    }
                    break;

                case NodeType.Condition:
                    {
                        IReadOnlyList<DataWrapper> portDatas = nodeData.portDatas;

                        bool found = false;

                        for (int i = 1; i < portDatas.Count; i++)
                        {
                            bool allExist = true;
                            IReadOnlyList<GenericData> datas = portDatas[i].data;

                            for (int j = 0; j < datas.Count; j++)
                            {
                                if (!runner.IsTrigger(datas[j].ToString()))
                                {
                                    allExist = false;

                                    break;
                                }
                            }

                            if (allExist)
                            {
                                RunnerPort port = new(currentNode, i);

                                if (links.TryGetValue(port, out RunnerPort target))
                                {
                                    currentNode = target.guid;

                                    found = true;

                                    break;
                                }
                            }
                        }

                        if (!found)
                        {
                            RunnerPort port = new(currentNode, 0);

                            if (links.TryGetValue(port, out RunnerPort target)) return target.guid;
                        }
                    }
                    break;

                case NodeType.Trigger:
                    {
                        IReadOnlyList<DataWrapper> optionDatas = nodeData.optionDatas;

                        for (int i = 0; i < optionDatas.Count; i++)
                        {
                            IReadOnlyList<GenericData> datas = optionDatas[i].data;

                            string value = datas[0].ToString();

                            if (value == "") continue;

                            if (datas[1].TryGetBool(out bool isTrigger))
                            {
                                if (isTrigger) runner.SetTrigger(value);
                                else runner.UnsetTrigger(value);
                            }
                        }

                        RunnerPort port = new(currentNode, 0);

                        if (links.TryGetValue(port, out RunnerPort target)) return target.guid;
                    }
                    break;

                case NodeType.Event:
                    {
                        IReadOnlyList<DataWrapper> optionDatas = nodeData.optionDatas;

                        for (int i = 0; i < optionDatas.Count; i++)
                        {
                            IReadOnlyList<GenericData> datas = optionDatas[i].data;

                            string value = datas[0].ToString();

                            if (value == "") continue;

                            runnerEvent.OnEvent(value);
                        }

                        RunnerPort port = new(currentNode, 0);

                        if (links.TryGetValue(port, out RunnerPort target)) return target.guid;
                    }
                    break;

                case NodeType.Choice:
                    {
                        CurrentNode = currentNode;

                        runnerDatas.Clear();

                        IReadOnlyList<DataWrapper> textEntries = nodeData.portDatas;

                        IReadOnlyList<GenericData> speakerTable = nodeData.optionDatas[0].data;
                        IReadOnlyList<GenericData> speakerEntry = nodeData.optionDatas[1].data;

                        IReadOnlyList<GenericData> textTable = nodeData.optionDatas[2].data;

                        IReadOnlyList<GenericData> message = nodeData.optionDatas[3].data;

                        RunnerText speaker = new(speakerTable[0].ToString(), speakerEntry[0].ToString());

                        for (int i = 0; i < textEntries.Count; i++)
                        {
                            RunnerText data = new(i, textTable[0].ToString(), textEntries[i].data[0].ToString());

                            runnerDatas.Add(data);
                        }

                        foreach (IDialogueView view in runner.Views)
                        {
                            int result = await view.OnChoice(speaker, runnerDatas, message[0].ToString(), token);

                            if (result != -1)
                            {
                                RunnerPort port = new(currentNode, runnerDatas[result].portIndex);

                                if (links.TryGetValue(port, out RunnerPort target))
                                {
                                    CurrentNode = target.guid;

                                    return CurrentNode;
                                }
                                break;
                            }
                        }
                    }
                    break;

                case NodeType.Wait:
                    {
                        CurrentNode = currentNode;

                        IReadOnlyList<GenericData> datas = nodeData.optionDatas[0].data;

                        if (datas[0].TryGetEnum(out WaitType type))
                        {
                            switch (type)
                            {
                                case WaitType.Notify:
                                    bool isWait = true;

                                    foreach (IDialogueView view in runner.Views) StopCallback?.Invoke(datas[1].ToString(), () => isWait = false);

                                    runnerEvent.OnEvent(datas[1].ToString());

                                    while (isWait && !token.IsStop) await Task.Yield();
                                    break;

                                case WaitType.Seconds:
                                    if (datas[1].TryGetFloat(out float second)) await token.Delay(second);
                                    break;
                            }
                        }

                        RunnerPort port = new(currentNode, 0);

                        if (links.TryGetValue(port, out RunnerPort target))
                        {
                            CurrentNode = target.guid;

                            return CurrentNode;
                        }
                    }
                    break;

                case NodeType.Object:
                    {
                        CurrentNode = currentNode;

                        runnerObjects.Clear();

                        IReadOnlyList<DataWrapper> optionDatas = nodeData.optionDatas;

                        for (int i = 0; i < optionDatas.Count; i++)
                        {
                            IReadOnlyList<GenericData> datas = optionDatas[i].data;

                            if (datas[0].TryGetObject(out UnityEngine.Object value)) runnerObjects.Add(value);
                        }

                        foreach (IDialogueView view in runner.Views) await view.OnObject(runnerObjects, token);

                        RunnerPort port = new(currentNode, 0);

                        if (links.TryGetValue(port, out RunnerPort target))
                        {
                            CurrentNode = target.guid;

                            return CurrentNode;
                        }
                    }
                    break;
            }

            if (token.IsStop) StopCallback?.Invoke("", null);
            else
            {
                EndCallback?.Invoke();
                CurrentNode = "";
            }

            return "";
        }

        public bool JumpNode(string nodeName)
        {
            if (nodeName == "" && !nodes.ContainsKey(nodeName)) return false;

            CurrentNode = nodeName;

            return true;
        }
    }
}