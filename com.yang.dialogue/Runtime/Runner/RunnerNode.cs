using System.Collections.Generic;
using UnityEngine;

namespace Yang.Dialogue
{
    internal class RunnerNode
    {
        private RunnerEvent runnerEvent;
        private RunnerTrigger runnerTrigger;
        private NodeData[] runtimeNodes = System.Array.Empty<NodeData>();
        private int[][] runtimeLinks = System.Array.Empty<int[]>();
        private RunnerCommand[][] commandCache = System.Array.Empty<RunnerCommand[]>();
        private RunnerCommand[][] eventCache = System.Array.Empty<RunnerCommand[]>();

        private readonly Dictionary<string, NodeData> nodes = new();
        private readonly Dictionary<RunnerPort, RunnerPort> links = new();
        private readonly Dictionary<string, int> nodeIndices = new();

        public void Init(RunnerEvent runnerEvent, RunnerTrigger runnerTrigger)
        {
            this.runnerEvent = runnerEvent;
            this.runnerTrigger = runnerTrigger;
        }

        public void SetDatas(DialogueSO so)
        {
            if (so == null) return;
            so.GetDatas(nodes, links);
            nodeIndices.Clear();
            runtimeNodes = new NodeData[nodes.Count];
            int nodeIndex = 0;

            foreach (KeyValuePair<string, NodeData> pair in nodes)
            {
                nodeIndices.Add(pair.Key, nodeIndex);
                runtimeNodes[nodeIndex++] = pair.Value;
            }

            int[] maxPortIndices = new int[runtimeNodes.Length];
            System.Array.Fill(maxPortIndices, -1);

            foreach (KeyValuePair<RunnerPort, RunnerPort> pair in links)
            {
                int sourceIndex = nodeIndices[pair.Key.guid];
                if (pair.Key.portIndex > maxPortIndices[sourceIndex])
                    maxPortIndices[sourceIndex] = pair.Key.portIndex;
            }

            runtimeLinks = new int[runtimeNodes.Length][];

            for (int i = 0; i < runtimeLinks.Length; i++)
            {
                int portCount = maxPortIndices[i] + 1;

                if (portCount == 0)
                {
                    runtimeLinks[i] = System.Array.Empty<int>();
                    continue;
                }

                int[] targets = new int[portCount];
                System.Array.Fill(targets, -1);
                runtimeLinks[i] = targets;
            }

            commandCache = new RunnerCommand[runtimeNodes.Length][];
            eventCache = new RunnerCommand[runtimeNodes.Length][];

            foreach (KeyValuePair<RunnerPort, RunnerPort> pair in links)
            {
                int sourceIndex = nodeIndices[pair.Key.guid];
                int targetIndex = nodeIndices[pair.Value.guid];
                runtimeLinks[sourceIndex][pair.Key.portIndex] = targetIndex;
            }

            for (int i = 0; i < runtimeNodes.Length; i++)
            {
                NodeData node = runtimeNodes[i];
                if (node.type == NodeType.Command)
                    commandCache[i] = CompileCommands(node.OptionDatas);
                else if (node.type == NodeType.Event)
                    eventCache[i] = CompileCommands(node.OptionDatas);
            }

            nodes.Clear();
            links.Clear();
        }

        private static RunnerCommand[] CompileCommands(IReadOnlyList<DataWrapper> optionDatas)
        {
            List<RunnerCommand> commands = new(optionDatas.Count);

            for (int i = 0; i < optionDatas.Count; i++)
            {
                IReadOnlyList<GenericData> datas = optionDatas[i].data;
                if (datas.Count == 0 || !datas[0].TryGetString(out string id) || string.IsNullOrWhiteSpace(id)) continue;
                int writeIndex = 0;
                int argumentCount = (datas.Count - 1) / 2;
                RunnerArgument[] arguments = new RunnerArgument[argumentCount];
                HashSet<int> fieldIds = new(argumentCount);

                for (int j = 1; j + 1 < datas.Count; j += 2)
                {
                    if (!datas[j].TryGetInt(out int fieldId) || fieldId <= 0 || !fieldIds.Add(fieldId)) continue;
                    arguments[writeIndex++] = new RunnerArgument(fieldId, datas[j + 1]);
                }

                if (writeIndex != arguments.Length)
                    System.Array.Resize(ref arguments, writeIndex);
                commands.Add(new RunnerCommand(id, arguments));
            }

            return commands.ToArray();
        }

        public async Awaitable<int> NextNode(RunnerToken token)
        {
            NodeData nodeData = runtimeNodes[token.NodeIndex];

            switch (nodeData.type)
            {
                case NodeType.Start:
                    return 0;
                case NodeType.Dialogue:
                    {
                        IReadOnlyList<GenericData> speakerTable = nodeData.OptionDatas[0].data;
                        IReadOnlyList<GenericData> speakerEntry = nodeData.OptionDatas[1].data;
                        IReadOnlyList<GenericData> textTable = nodeData.OptionDatas[2].data;
                        IReadOnlyList<GenericData> textEntry = nodeData.OptionDatas[3].data;
                        IReadOnlyList<GenericData> message = nodeData.OptionDatas[4].data;
                        RunnerText speaker = new(speakerTable[0].ToString(), speakerEntry[0].ToString(), speakerTable[1].GetGuid(), speakerEntry[1].GetLong());
                        RunnerText text = new(textTable[0].ToString(), textEntry[0].ToString(), textTable[1].GetGuid(), textEntry[1].GetLong());

                        for (int i = 0; i < token.Views.Count; i++)
                        {
                            await token.Views[i].OnDialogue(speaker, text, message[0].ToString(), token);
                            if (token.State != TokenState.Running) return -1;
                        }
                    }
                    return 0;
                case NodeType.Condition:
                    {
                        IReadOnlyList<DataWrapper> portDatas = nodeData.PortDatas;

                        for (int i = 1; i < portDatas.Count; i++)
                        {
                            bool allExist = true;
                            IReadOnlyList<GenericData> datas = portDatas[i].data;

                            for (int j = 0; j < datas.Count; j += 3)
                            {
                                int fieldId = datas[j].GetInt();

                                switch (datas[j + 1].Type)
                                {
                                    case GenericData.DataType.Float:
                                        {
                                            float value = runnerTrigger.GetFloatValue(fieldId);
                                            float checkValue = datas[j + 1].GetFloat();
                                            ValueCheckType type = datas[j + 2].GetEnum<ValueCheckType>();
                                            if (!CheckValue(value, checkValue, type))
                                                allExist = false;
                                        }
                                        break;
                                    case GenericData.DataType.Bool:
                                        {
                                            bool value = runnerTrigger.GetBoolValue(fieldId);
                                            bool checkValue = datas[j + 1].GetBool();
                                            if (value != checkValue)
                                                allExist = false;
                                        }
                                        break;
                                }

                                if (!allExist) break;
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
                            int fieldId = datas[0].GetInt();
                            if (fieldId <= 0) continue;

                            switch (datas[1].Type)
                            {
                                case GenericData.DataType.Float:
                                    switch (datas[2].GetEnum<ValueSetterType>())
                                    {
                                        case ValueSetterType.Plus:
                                            {
                                                float value = runnerTrigger.GetFloatValue(fieldId);
                                                runnerTrigger.SetValue(fieldId, value + datas[1].GetFloat());
                                            }
                                            break;
                                        case ValueSetterType.Minus:
                                            {
                                                float value = runnerTrigger.GetFloatValue(fieldId);
                                                runnerTrigger.SetValue(fieldId, value - datas[1].GetFloat());
                                            }
                                            break;
                                        case ValueSetterType.Set:
                                            runnerTrigger.SetValue(fieldId, datas[1].GetFloat());
                                            break;
                                    }

                                    break;
                                case GenericData.DataType.Bool:
                                    runnerTrigger.SetValue(fieldId, datas[1].GetBool());
                                    break;
                            }

                            if (token.State != TokenState.Running) return -1;
                        }
                    }
                    return 0;
                case NodeType.Event:
                    {
                        RunnerCommand[] events = eventCache[token.NodeIndex];

                        for (int i = 0; i < events.Length; i++)
                        {
                            runnerEvent.OnEvent(events[i]);
                            if (token.State != TokenState.Running) return -1;
                        }
                    }
                    return 0;
                case NodeType.Choice:
                    {
                        IReadOnlyList<DataWrapper> textEntries = nodeData.PortDatas;
                        IReadOnlyList<GenericData> speakerTable = nodeData.OptionDatas[0].data;
                        IReadOnlyList<GenericData> speakerEntry = nodeData.OptionDatas[1].data;
                        IReadOnlyList<GenericData> textTable = nodeData.OptionDatas[2].data;
                        IReadOnlyList<GenericData> message = nodeData.OptionDatas[3].data;
                        RunnerText speaker = new(speakerTable[0].ToString(), speakerEntry[0].ToString(), speakerTable[1].GetGuid(), speakerEntry[1].GetLong());
                        string textTableKey = textTable[0].ToString();
                        System.Guid textTableGuid = textTable[1].GetGuid();
                        RunnerToken runnerToken = (RunnerToken)token;
                        RunnerChoiceCollection choiceDatas = runnerToken.GetChoiceCache(nodeData.guid, textEntries);
                        int choiceIndex = 0;

                        for (int i = 0; i < textEntries.Count; i++)
                        {
                            IReadOnlyList<GenericData> textEntry = textEntries[i].data;
                            bool hide = textEntry[2].GetBool();
                            bool isValid = true;
                            string textEntryKey = textEntry[0].ToString();
                            RunnerCondition[] conditions = choiceDatas.ConditionBuffers[i];

                            for (int j = 0; j < conditions.Length; j++)
                            {
                                int dataIndex = 3 + (j * 3);
                                int fieldId = textEntry[dataIndex].GetInt();

                                switch (textEntry[dataIndex + 1].Type)
                                {
                                    case GenericData.DataType.Float:
                                        {
                                            float value = runnerTrigger.GetFloatValue(fieldId);
                                            float checkValue = textEntry[dataIndex + 1].GetFloat();
                                            ValueCheckType type = textEntry[dataIndex + 2].GetEnum<ValueCheckType>();
                                            bool check = CheckValue(value, checkValue, type);
                                            if (!check)
                                                isValid = false;
                                            conditions[j] = new(fieldId, check, checkValue, type);
                                        }
                                        break;
                                    case GenericData.DataType.Bool:
                                        {
                                            bool value = runnerTrigger.GetBoolValue(fieldId);
                                            bool checkValue = textEntry[dataIndex + 1].GetBool();
                                            bool check = value == checkValue;
                                            if (!check)
                                                isValid = false;
                                            conditions[j] = new(fieldId, check, checkValue);
                                        }
                                        break;
                                }
                            }

                            if (hide && !isValid) continue;
                            choiceDatas.Set(choiceIndex++, new RunnerChoiceText(i, textTableKey, textEntryKey, textTableGuid, textEntry[1].GetLong(), isValid, conditions));
                        }

                        choiceDatas.SetCount(choiceIndex);
                        if (choiceIndex == 0) return -1;
                        int index = -1;

                        foreach (IDialogueView view in token.Views)
                        {
                            int result = await view.OnChoice(speaker, choiceDatas, message[0].ToString(), token);
                            if (token.State != TokenState.Running) return -1;
                            if (result >= 0 && result < choiceIndex && choiceDatas[result].isValid)
                                index = result;
                        }

                        return index < 0 ? -1 : choiceDatas[index].portIndex;
                    }
                case NodeType.Wait:
                    {
                        IReadOnlyList<GenericData> datas = nodeData.OptionDatas[0].data;
                        if (datas[1].TryGetFloat(out float second))
                            await token.Delay(second);
                        else
                        {
                            for (int i = 0; i < token.Views.Count; i++)
                            {
                                await token.Views[i].OnMessage(datas[1].ToString(), token);
                                if (token.State != TokenState.Running) return -1;
                            }
                        }
                    }
                    return 0;
                case NodeType.Command:
                    {
                        RunnerCommand[] commands = commandCache[token.NodeIndex];

                        for (int i = 0; i < token.Views.Count; i++)
                        {
                            await token.Views[i].OnCommand(commands, token);
                            if (token.State != TokenState.Running) return -1;
                        }
                    }
                    return 0;
            }

            return -1;
        }

        public bool TryGetNodeType(string guid, out NodeType type)
        {
            if (guid != null && nodeIndices.TryGetValue(guid, out int index))
            {
                type = runtimeNodes[index].type;
                return true;
            }

            type = default;
            return false;
        }

        public bool CheckNode(string nodeName)
        {
            if (string.IsNullOrEmpty(nodeName) || !nodeIndices.ContainsKey(nodeName)) return false;
            return true;
        }

        public int GetNodeIndex(string nodeName) => nodeIndices.TryGetValue(nodeName, out int index) ? index : -1;

        public string GetNodeGuid(int nodeIndex) => runtimeNodes[nodeIndex].guid;

        public bool TryGetLink(int nodeIndex, int portIndex, out int targetIndex)
        {
            int[] portLinks = runtimeLinks[nodeIndex];

            if ((uint)portIndex >= (uint)portLinks.Length)
            {
                targetIndex = -1;
                return false;
            }

            targetIndex = portLinks[portIndex];
            return targetIndex >= 0;
        }

        private bool CheckValue(float value, float checkValue, ValueCheckType type)
        {
            switch (type)
            {
                case ValueCheckType.Less:
                    if (value >= checkValue) return false;
                    break;
                case ValueCheckType.Equal:
                    if (!Mathf.Approximately(checkValue, value)) return false;
                    break;
                case ValueCheckType.LessEqual:
                    if (value > checkValue) return false;
                    break;
                case ValueCheckType.Greater:
                    if (value <= checkValue) return false;
                    break;
                case ValueCheckType.NotEqual:
                    if (Mathf.Approximately(checkValue, value)) return false;
                    break;
                case ValueCheckType.GreaterEqual:
                    if (value < checkValue) return false;
                    break;
            }

            return true;
        }
    }
}
