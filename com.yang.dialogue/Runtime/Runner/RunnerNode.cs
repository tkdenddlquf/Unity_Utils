using System.Collections.Generic;
using System.Threading.Tasks;

namespace Yang.Dialogue
{
    internal class RunnerNode
    {
        private RunnerEvent runnerEvent;
        private RunnerTrigger runnerTrigger;

        private readonly List<RunnerText> runnerDatas = new();
        private readonly List<UnityEngine.Object> runnerObjects = new();

        private readonly Dictionary<string, NodeData> nodes = new();
        private readonly Dictionary<RunnerPort, RunnerPort> links = new();

        public void Init(RunnerEvent runnerEvent, RunnerTrigger runnerTrigger)
        {
            this.runnerEvent = runnerEvent;
            this.runnerTrigger = runnerTrigger;
        }

        public void SetDatas(DialogueSO so)
        {
            if (so == null) return;

            so.GetDatas(nodes, links);
        }

        public async Task<int> NextNode(IReadOnlyList<IDialogueView> views, IRunnerNodeChecker checker, IRunnerToken token)
        {
            NodeData nodeData = nodes[checker.TargetNode];

            switch (nodeData.type)
            {
                case NodeType.Start:
                    return 0;

                case NodeType.Dialogue:
                    {
                        checker.PointSave();

                        IReadOnlyList<GenericData> speakerTable = nodeData.OptionDatas[0].data;
                        IReadOnlyList<GenericData> speakerEntry = nodeData.OptionDatas[1].data;

                        IReadOnlyList<GenericData> textTable = nodeData.OptionDatas[2].data;
                        IReadOnlyList<GenericData> textEntry = nodeData.OptionDatas[3].data;

                        IReadOnlyList<GenericData> message = nodeData.OptionDatas[4].data;

                        RunnerText speaker = new(speakerTable[0].ToString(), speakerEntry[0].ToString());
                        RunnerText text = new(textTable[0].ToString(), textEntry[0].ToString());

                        foreach (IDialogueView view in views) await view.OnDialogue(speaker, text, message[0].ToString(), token);
                    }
                    return 0;

                case NodeType.Condition:
                    {
                        IReadOnlyList<DataWrapper> portDatas = nodeData.PortDatas;

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

                            if (allExist) return i;
                        }
                    }
                    return 0;

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
                    }
                    return 0;

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
                    }
                    return 0;

                case NodeType.Choice:
                    {
                        checker.PointSave();

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

                            return runnerDatas[result].portIndex;
                        }
                    }
                    break;

                case NodeType.Wait:
                    {
                        checker.PointSave();

                        IReadOnlyList<GenericData> datas = nodeData.OptionDatas[0].data;

                        if (datas[1].TryGetFloat(out float second)) await token.Delay(second);
                        else
                        {
                            foreach (IDialogueView view in views) await view.OnMessage(datas[1].ToString(), token);
                        }
                    }
                    return 0;

                case NodeType.Object:
                    {
                        checker.PointSave();

                        runnerObjects.Clear();

                        IReadOnlyList<DataWrapper> optionDatas = nodeData.OptionDatas;

                        for (int i = 0; i < optionDatas.Count; i++)
                        {
                            IReadOnlyList<GenericData> datas = optionDatas[i].data;

                            if (datas[0].TryGetObject(out UnityEngine.Object value)) runnerObjects.Add(value);
                        }

                        foreach (IDialogueView view in views) await view.OnObject(runnerObjects, token);
                    }
                    return 0;
            }

            return -1;
        }

        public bool CheckNode(string nodeName)
        {
            if (nodeName == "" && !nodes.ContainsKey(nodeName)) return false;

            return true;
        }

        public bool TryGetLink(RunnerPort port, out string nodeName)
        {
            if (links.TryGetValue(port, out RunnerPort targetPort))
            {
                nodeName = targetPort.guid;

                return true;
            }

            nodeName = "";

            return false;
        }
    }
}