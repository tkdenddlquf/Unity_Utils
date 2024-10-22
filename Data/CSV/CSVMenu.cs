#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;

public class CSVMenu
{
    [MenuItem("CSVReader/Parse Dialog")]
    public static void Read()
    {
        CryptData crypt = new();
        List<DialogInfo> dialogDatas = new();

        foreach (var _data in CSVReader.Parse("Assets/#Datas/CSV/Dialog.csv"))
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
}
#endif