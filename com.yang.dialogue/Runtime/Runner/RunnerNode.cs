using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using UnityEngine;

namespace Yang.Dialogue
{
    internal class RunnerNode
    {
        private RunnerEvent runnerEvent;
        private RunnerTrigger runnerTrigger;

        private readonly List<RunnerChoiceText> choiceDatas = new();
        private readonly List<UnityEngine.Object> objectDatas = new();

        private readonly ReadOnlyCollection<RunnerChoiceText> choiceDatasView;
        private readonly ReadOnlyCollection<UnityEngine.Object> objectDatasView;

        private readonly Dictionary<string, NodeData> nodes = new();
        private readonly Dictionary<RunnerPort, RunnerPort> links = new();

        public RunnerNode()
        {
            choiceDatasView = choiceDatas.AsReadOnly();
            objectDatasView = objectDatas.AsReadOnly();
        }

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

                            for (int j = 0; j < datas.Count; j += 3)
                            {
                                string key = datas[j].ToString();

                                switch (datas[j + 1].Type)
                                {
                                    case GenericData.DataType.Float:
                                        {
                                            float value = runnerTrigger.GetFloatValue(key);
                                            float checkValue = datas[j + 1].GetFloat();

                                            ValueCheckType type = datas[j + 2].GetEnum<ValueCheckType>();

                                            if (!CheckValue(value, checkValue, type)) allExist = false;
                                        }
                                        break;

                                    case GenericData.DataType.Bool:
                                        {
                                            bool value = runnerTrigger.GetBoolValue(key);
                                            bool checkValue = datas[j + 1].GetBool();

                                            if (value != checkValue) allExist = false;
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

                            string key = datas[0].ToString();

                            if (key == "") continue;

                            switch (datas[1].Type)
                            {
                                case GenericData.DataType.Float:
                                    switch (datas[2].GetEnum<ValueSetterType>())
                                    {
                                        case ValueSetterType.Plus:
                                            {
                                                float value = runnerTrigger.GetFloatValue(key);

                                                runnerTrigger.SetValue(key, value + datas[1].GetFloat());
                                            }
                                            break;

                                        case ValueSetterType.Minus:
                                            {
                                                float value = runnerTrigger.GetFloatValue(key);

                                                runnerTrigger.SetValue(key, value - datas[1].GetFloat());
                                            }
                                            break;

                                        case ValueSetterType.Set:
                                            runnerTrigger.SetValue(key, datas[1].GetFloat());
                                            break;
                                    }
                                    break;

                                case GenericData.DataType.Bool:
                                    runnerTrigger.SetValue(key, datas[1].GetBool());
                                    break;
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

                        choiceDatas.Clear();

                        IReadOnlyList<DataWrapper> textEntries = nodeData.PortDatas;

                        IReadOnlyList<GenericData> speakerTable = nodeData.OptionDatas[0].data;
                        IReadOnlyList<GenericData> speakerEntry = nodeData.OptionDatas[1].data;

                        IReadOnlyList<GenericData> textTable = nodeData.OptionDatas[2].data;

                        IReadOnlyList<GenericData> message = nodeData.OptionDatas[3].data;

                        RunnerText speaker = new(speakerTable[0].ToString(), speakerEntry[0].ToString());

                        string textTableKey = textTable[0].ToString();

                        for (int i = 0; i < textEntries.Count; i++)
                        {
                            IReadOnlyList<GenericData> textEntry = textEntries[i].data;

                            bool hide = textEntry[2].GetBool();

                            if (hide) continue;

                            bool isValid = true;

                            string textEntryKey = textEntry[0].ToString();

                            int conditionCount = (textEntry.Count - 3) / 3;

                            RunnerCondition[] conditions = new RunnerCondition[conditionCount];

                            for (int j = 0; j < conditionCount; j++)
                            {
                                int dataIndex = 3 + (j * 3);

                                string key = textEntry[dataIndex].ToString();

                                switch (textEntry[dataIndex + 1].Type)
                                {
                                    case GenericData.DataType.Float:
                                        {
                                            float value = runnerTrigger.GetFloatValue(key);
                                            float checkValue = textEntry[dataIndex + 1].GetFloat();

                                            ValueCheckType type = textEntry[dataIndex + 2].GetEnum<ValueCheckType>();

                                            bool check = CheckValue(value, checkValue, type);

                                            if (!check) isValid = false;

                                            conditions[j] = new(key, check, checkValue, type);
                                        }
                                        break;

                                    case GenericData.DataType.Bool:
                                        {
                                            bool value = runnerTrigger.GetBoolValue(key);
                                            bool checkValue = textEntry[dataIndex + 1].GetBool();

                                            bool check = value != checkValue;

                                            if (!check) isValid = false;

                                            conditions[j] = new(key, check, checkValue);
                                        }
                                        break;
                                }
                            }

                            RunnerChoiceText data = new(i, textTableKey, textEntryKey, isValid, conditions);

                            choiceDatas.Add(data);
                        }

                        int index = 0;

                        foreach (IDialogueView view in views)
                        {
                            int result = await view.OnChoice(speaker, choiceDatasView, message[0].ToString(), token);

                            if (result != -1) index = result;
                        }

                        return choiceDatas[index].portIndex;
                    }

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

                        objectDatas.Clear();

                        IReadOnlyList<DataWrapper> optionDatas = nodeData.OptionDatas;

                        for (int i = 0; i < optionDatas.Count; i++)
                        {
                            IReadOnlyList<GenericData> datas = optionDatas[i].data;

                            if (datas[0].TryGetObject(out UnityEngine.Object value)) objectDatas.Add(value);
                        }

                        foreach (IDialogueView view in views) await view.OnObject(objectDatasView, token);
                    }
                    return 0;
            }

            return -1;
        }

        public bool CheckNode(string nodeName)
        {
            if (nodeName == "" || !nodes.ContainsKey(nodeName)) return false;

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

        private bool CheckValue(float value, float checkValue, ValueCheckType type)
        {
            switch (type)
            {
                case ValueCheckType.Less:
                    if (checkValue >= value) return false;
                    break;

                case ValueCheckType.Equal:
                    if (!Mathf.Approximately(checkValue, value)) return false;
                    break;

                case ValueCheckType.LessEqual:
                    if (checkValue > value) return false;
                    break;

                case ValueCheckType.Greater:
                    if (checkValue <= value) return false;
                    break;

                case ValueCheckType.NotEqual:
                    if (Mathf.Approximately(checkValue, value)) return false;
                    break;

                case ValueCheckType.GreaterEqual:
                    if (checkValue < value) return false;
                    break;
            }

            return true;
        }
    }
}