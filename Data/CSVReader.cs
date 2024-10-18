#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.IO;
using UnityEditor;

public class CSVReader
{
    private static readonly string SPLIT_RE = @",(?=(?:[^""]*""[^""]*"")*(?![^""]*""))";
    private static readonly string LINE_SPLIT_RE = @"\r\n|\n\r|\n|\r";
    private static readonly char[] TRIM_CHARS = { '\"' };

    [MenuItem("CSVReader/Parse Dialog")]
    public static void Read()
    {
        CryptData crypt = new();
        List<DialogInfo> dialogDatas = new();

        foreach (var _data in Parse("Assets/#Datas/CSV/Dialog.csv"))
        {
            dialogDatas.Add(new DialogInfo()
            {
                meshNum = (int)_data["대상"],
                startAction = (int)_data["시작 액션"],
                endAction = (int)_data["종료 액션"],
                sleepTime = (float)_data["속도"],
                text = (string)_data["대사"]
            });
        }

        crypt.Save(new DialogDatas { data = dialogDatas.ToArray() }, "Dialog/DialogData.json", CryptType.Resources);
    }

    public static List<Dictionary<string, object>> Parse(string _path)
    {
        List<Dictionary<string, object>> _list = new();

        string _data = File.ReadAllText(_path, System.Text.Encoding.UTF8);

        string[] _lines = Regex.Split(_data, LINE_SPLIT_RE);

        if (_lines.Length <= 1) return _list;

        string[] _header = Regex.Split(_lines[0], SPLIT_RE);

        for (int i = 1; i < _lines.Length; i++)
        {
            string[] _values = Regex.Split(_lines[i], SPLIT_RE);

            if (_values.Length == 0 || _values[0] == "") continue;

            Dictionary<string, object> _entry = new();

            for (int j = 0; j < _header.Length && j < _values.Length; j++)
            {
                string _value = _values[j];

                _value = _value.TrimStart(TRIM_CHARS).TrimEnd(TRIM_CHARS).Replace("\\", "");

                object _finalvalue = _value;

                if (int.TryParse(_value, out int n)) _finalvalue = n;
                else if (float.TryParse(_value, out float f)) _finalvalue = f;

                _entry[_header[j]] = _finalvalue;
            }

            _list.Add(_entry);
        }

        return _list;
    }
}
#endif