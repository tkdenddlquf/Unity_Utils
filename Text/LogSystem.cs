using UnityEngine;

public class LogSystem : MonoBehaviour
{
    public LogInfo[] logInfos;

    private int logIndex;

    public void Notice_Log(string _text) // 로그 출력 및 기록
    {
        bool _notice = false;

        for (int i = 0; i < logInfos.Length; i++)
        {
            if (logInfos[i].gameObject.activeSelf) continue; // 로그가 활성화 된 경우 생략

            _notice = true;
            logIndex = i;

            logInfos[i].SetText(_text);
            break;
        }

        if (!_notice) // 로그를 출력할 수 없는 경우
        {
            if (logIndex == logInfos.Length - 1) logIndex = 0;
            else logIndex++;

            logInfos[logIndex].SetText(_text);
        }
    }
}
