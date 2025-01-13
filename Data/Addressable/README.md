# 설명
Addressable 기능을 편리하게 사용하기 위해 제작

# 사용
1. 총 2개의 Handle 사용하며 각 Handle는 IsComplete를 포함하며 완료 여부 확인 가능
   - ResourceDownloadHandle
     - Download 해야하는 전체 용량 및 진행 정도 확인 가능
   - ResourceLoadHandle
     - Asset Load 및 Callback 호출

2. ResourceDownloadHandle
   - CheckDownloadSize()에 확인 할 Bundle의 이름을 전달하여 전체 용량 확인
     - 용량 확인이 끝난 경우 IsComplete가 true로 전환
     - TotalSize를 호출하여 전체 용량 확인 가능
   - Download()로 Bundle Download 진행
     - 이전에 CheckDownloadSize()에서 확인된 Bundle Download
     - GetDownloadPercent() 를 호출하여 현재 진행률 확인 가능

3. ResourceLoadHandle
   - LoadAsset()에 경로 및 Callback을 전달하여 Asset Load
     - Asset Load가 완료된 경우 Callback 호출