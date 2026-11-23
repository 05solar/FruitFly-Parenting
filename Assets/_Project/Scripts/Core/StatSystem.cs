using System;
using System.Collections.Generic;
using FruitFlyParenting.Data;

namespace FruitFlyParenting.Core
{
    // 책임 분리: 이 클래스는 0~100 clamp와 변경 이벤트 발행만 담당한다.
    // 방 단위 최대 변화량 제한(일반 물품 ±12, ★ 물품 ±25, 스펙 4.4/4.5)은
    // "물품 효과 적용"이라는 상위 개념이므로 05번 이후 ItemEffect 쪽의 책임이다.
    public class StatSystem
    {
        public event Action<StatType, float, float, string> OnStatChanged;

        readonly Dictionary<StatType, float> values = new Dictionary<StatType, float>();
        readonly float min;
        readonly float max;

        public StatSystem(GameBalanceConfig config)
        {
            min = config.statMin;
            max = config.statMax;

            foreach (StatType type in (StatType[])Enum.GetValues(typeof(StatType)))
            {
                float initial = type == StatType.Rebellion
                    ? config.rebellionInitialValue
                    : config.statDefaultValue;
                values[type] = Clamp(initial);
            }
        }

        public float GetValue(StatType type) => values[type];

        // 모든 수치 변경은 이 메서드 하나만을 통과해야 한다. 외부에서 Dictionary를 직접 건드리지 않는다.
        public void ApplyDelta(StatType type, float amount, string reasonTag)
        {
            float oldValue = values[type];
            float newValue = Clamp(oldValue + amount);
            values[type] = newValue;
            OnStatChanged?.Invoke(type, oldValue, newValue, reasonTag);
        }

        float Clamp(float value) => Math.Min(max, Math.Max(min, value));
    }
}
