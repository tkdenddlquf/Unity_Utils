# 설명
값을 바인딩 할 때 사용하며 기본적으로
기존 값에 새로운 값을 적용하는 Callback이 할당 되어있음

# 사용
1. new()로 생성하며 생성 시 인자값을 전달하며 기본 값 설정 가능

2. Value 프로퍼티를 호출하여 사용하며, 값을 변경 시 2종류의 Callback을 호출
   - PrevCallback(ref T currentValue, T newValue)
     - 대입되는 값을 조절 하는 Callback이 필요한 경우 사용
   - SubCallback(T newValue)
     - 변경이 완료된 이후 Callback이 필요한 경우 사용

3. SetCallback()으로 Callback을 설정하며 Type 지정 가능
   - Callback 설정 이후 설정된 Callback을 Invoke함
   - Type은 3종류로 구분
     - SetCallbackType.Set
       - Callback에 대입
     - SetCallbackType.Add
       - Callback에 추가
     - SetCallbackType.Remove
       - Callback에서 제거, Invoke 하지 않고 Return