using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Yang.Dialogue
{
    public class RunnerNode
    {
        public string CurrentNode { get; private set; }

        public bool IsWait { get; private set; }

        private readonly List<RunnerText> runnerDatas = new();

        private readonly DialogueRunner runner;
        private readonly RunnerEvent runnerEvent;

        private readonly Dictionary<string, NodeData> nodes = new();
        private readonly Dictionary<RunnerPort, RunnerPort> links = new();

        public RunnerNode(DialogueRunner runner, RunnerEvent runnerEvent)
        {
            this.runner = runner;
            this.runnerEvent = runnerEvent;
        }

        public void SetDatas(DialogueSO so)
        {
            if (so == null) return;

            nodes.Clear();
            links.Clear();

            NodeData startNode = so.startNode;

            nodes.Add(startNode.guid, startNode);

            foreach (NodeData node in so.GetNodes()) nodes.Add(node.guid, node);

            foreach (LinkData link in so.GetLinks())
            {
                RunnerPort output = new(link.nodeGuid, link.portName);
                RunnerPort input = new(link.targetGuid, link.targetPortName);

                links.Add(output, input);
            }

            CurrentNode = startNode.guid;
        }

        public async Task<string> NextNode(string currentNode, RunnerToken token)
        {
            NodeData nodeData = nodes[currentNode];

            switch (nodeData.type)
            {
                case NodeType.Start:
                    {
                        string portName = nodeData.GetPort(0);

                        RunnerPort port = new(currentNode, portName);

                        if (links.TryGetValue(port, out RunnerPort target)) return target.guid;
                    }
                    break;

                case NodeType.Dialogue:
                    {
                        CurrentNode = currentNode;

                        int speakerTableIndex = nodeData.GetOptionIndex(DialogueType.DIALOGUE_TYPE_000, _ => _.Count != 0);
                        int speakerEntryIndex = nodeData.GetOptionIndex(DialogueType.DIALOGUE_TYPE_001, _ => _.Count != 0);

                        int textTableIndex = nodeData.GetOptionIndex(DialogueType.DIALOGUE_TYPE_002, _ => _.Count != 0);
                        int textEntryIndex = nodeData.GetOptionIndex(DialogueType.DIALOGUE_TYPE_003, _ => _.Count != 0);

                        int messageIndex = nodeData.GetOptionIndex(DialogueType.DIALOGUE_TYPE_004, _ => _.Count != 0);

                        OptionData speakerTable = nodeData.GetOption(speakerTableIndex);
                        OptionData speakerEntry = nodeData.GetOption(speakerEntryIndex);

                        OptionData textTable = nodeData.GetOption(textTableIndex);
                        OptionData textEntry = nodeData.GetOption(textEntryIndex);

                        OptionData message = nodeData.GetOption(messageIndex);

                        RunnerText speaker = new(speakerTable.datas[0], speakerEntry.datas[0]);
                        RunnerText text = new(nodeData.GetPort(0), textTable.datas[0], textEntry.datas[0]);

                        foreach (IDialogueView view in runner.Views) await view.OnDialogue(speaker, text, message.datas[0], token);

                        RunnerPort port = new(currentNode, text.portName);

                        if (links.TryGetValue(port, out RunnerPort target))
                        {
                            CurrentNode = target.guid;

                            return CurrentNode;
                        }
                    }
                    break;

                case NodeType.Condition:
                    {
                        IReadOnlyList<OptionData> options = nodeData.GetOptions();

                        bool found = false;

                        for (int i = 1; i < options.Count; i++)
                        {
                            bool allExist = true;
                            List<string> datas = options[i].datas;

                            for (int j = 1; j < datas.Count; j++)
                            {
                                if (!runner.IsTrigger(datas[j]))
                                {
                                    allExist = false;

                                    break;
                                }
                            }

                            if (allExist)
                            {
                                RunnerPort port = new(currentNode, datas[0]);

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
                            RunnerPort port = new(currentNode, options[0].datas[0]);

                            if (links.TryGetValue(port, out RunnerPort target)) return target.guid;
                        }
                    }
                    break;

                case NodeType.Trigger:
                    {
                        IReadOnlyList<OptionData> options = nodeData.GetOptions();

                        for (int i = 0; i < options.Count; i++)
                        {
                            List<string> datas = options[i].datas;

                            if (datas[1] == "") continue;

                            if (bool.TryParse(datas[2], out bool isTrigger))
                            {
                                if (isTrigger) runner.SetTrigger(datas[1]);
                                else runner.UnsetTrigger(datas[1]);
                            }
                        }

                        string portName = nodeData.GetPort(0);

                        RunnerPort port = new(currentNode, portName);

                        if (links.TryGetValue(port, out RunnerPort target)) return target.guid;
                    }
                    break;

                case NodeType.Event:
                    {
                        IReadOnlyList<OptionData> options = nodeData.GetOptions();

                        for (int i = 0; i < options.Count; i++)
                        {
                            List<string> datas = options[i].datas;

                            if (datas[1] == "") continue;

                            runnerEvent.OnEvent(datas[1]);
                        }

                        string portName = nodeData.GetPort(0);

                        RunnerPort port = new(currentNode, portName);

                        if (links.TryGetValue(port, out RunnerPort target)) return target.guid;
                    }
                    break;

                case NodeType.Choice:
                    {
                        CurrentNode = currentNode;

                        runnerDatas.Clear();

                        int speakerTableIndex = nodeData.GetOptionIndex(DialogueType.CHOICE_TYPE_000, _ => _.Count != 0);
                        int speakerEntryIndex = nodeData.GetOptionIndex(DialogueType.CHOICE_TYPE_001, _ => _.Count != 0);

                        int textTableIndex = nodeData.GetOptionIndex(DialogueType.CHOICE_TYPE_002, _ => _.Count != 0);

                        int messageIndex = nodeData.GetOptionIndex(DialogueType.CHOICE_TYPE_004, _ => _.Count != 0);

                        OptionData speakerTable = nodeData.GetOption(speakerTableIndex);
                        OptionData speakerEntry = nodeData.GetOption(speakerEntryIndex);

                        OptionData textTable = nodeData.GetOption(textTableIndex);

                        OptionData message = nodeData.GetOption(messageIndex);

                        RunnerText speaker = new(speakerTable.datas[0], speakerEntry.datas[0]);

                        foreach (OptionData textEntry in nodeData.GetOptions(DialogueType.CHOICE_TYPE_003, _ => _.Count != 0))
                        {
                            RunnerText data = new(textEntry.datas[0], textTable.datas[0], textEntry.datas[1]);

                            runnerDatas.Add(data);
                        }

                        foreach (IDialogueView view in runner.Views)
                        {
                            int result = await view.OnChoice(speaker, runnerDatas, message.datas[0], token);

                            if (result != -1)
                            {
                                RunnerPort port = new(currentNode, runnerDatas[result].portName);

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
                        IsWait = true;
                        CurrentNode = currentNode;

                        OptionData option = nodeData.GetOption(0);

                        List<string> datas = option.datas;

                        WaitType type = (WaitType)Enum.Parse(typeof(WaitType), datas[1]);

                        switch (type)
                        {
                            case WaitType.Notify:
                                foreach (IDialogueView view in runner.Views) view.OnNotify(NotifyType.Wait);
                                break;

                            case WaitType.Seconds:
                                TimeSpan delay = TimeSpan.FromSeconds(float.Parse(datas[2]));

                                await Task.Delay(delay.Milliseconds, token.Token);
                                break;
                        }

                        string portName = nodeData.GetPort(0);

                        RunnerPort port = new(currentNode, portName);

                        if (links.TryGetValue(port, out RunnerPort target))
                        {
                            CurrentNode = target.guid;

                            return CurrentNode;
                        }
                    }
                    break;
            }

            return "";
        }

        public void JumpNode(string nodeName)
        {
            if (nodeName == null && !nodes.ContainsKey(nodeName)) return;

            CurrentNode = nodeName;
        }

        public void Continue() => IsWait = false;
    }
}