using System;
using FruitFlyParenting.Data;
using FruitFlyParenting.Fly;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

namespace FruitFlyParenting.Room
{
    public enum GamblingPhase { None, SmallWin, PassingMonths, Loss, Complete }
    public sealed class GamblingMontage : MonoBehaviour
    {
        [SerializeField] GameBalanceConfig config;
        [SerializeField] FlyAgent fly;
        [SerializeField] Text caption;
        [SerializeField] Transform calendar;
        [SerializeField] GameObject coin;
        NavMeshAgent agent;
        bool wasEnabled,wasStopped;
        float elapsed;
        public GamblingPhase Phase { get; private set; }
        public bool IsPlaying=>Phase!=GamblingPhase.None && Phase!=GamblingPhase.Complete;
        public int CalendarMonth { get; private set; }
        public string Caption=>caption.text;
        public event Action Completed;
        public void Configure(GameBalanceConfig balance,FlyAgent actor,Text label,Transform pages,GameObject winningCoin)
        { config=balance; fly=actor; caption=label; calendar=pages; coin=winningCoin; }
        public bool Play()
        {
            if(Phase!=GamblingPhase.None) return false;
            agent=fly.GetComponent<NavMeshAgent>(); wasEnabled=fly.enabled; wasStopped=agent.isStopped;
            fly.enabled=false; agent.enabled=false; Phase=GamblingPhase.SmallWin; elapsed=0; Render(); return true;
        }
        void Update()
        {
            if(!IsPlaying) return;
            elapsed+=Time.deltaTime;
            if(elapsed>=config.gamblingWinSeconds+config.gamblingCalendarSeconds+config.gamblingLossSeconds)
            { Phase=GamblingPhase.Complete; Render(); Restore(); Completed?.Invoke(); return; }
            Phase=elapsed<config.gamblingWinSeconds ? GamblingPhase.SmallWin :
                elapsed<config.gamblingWinSeconds+config.gamblingCalendarSeconds ? GamblingPhase.PassingMonths : GamblingPhase.Loss;
            Render();
        }
        void Render()
        {
            coin.SetActive(Phase==GamblingPhase.SmallWin);
            if(Phase==GamblingPhase.SmallWin) caption.text="작은 당첨\n“한 번만 더 하면…”";
            else if(Phase==GamblingPhase.PassingMonths)
            {
                float progress=Mathf.Clamp01((elapsed-config.gamblingWinSeconds)/Mathf.Max(.01f,config.gamblingCalendarSeconds));
                CalendarMonth=Mathf.Clamp(1+Mathf.FloorToInt(progress*12),1,12);
                caption.text="다시 돌아온 날들이 쌓여요\n"+CalendarMonth+"개월째";
                calendar.localRotation=Quaternion.Euler(0,0,CalendarMonth%2==0 ? 4 : -4);
            }
            else { caption.text="생활비와 집을 잃었어요.\n남은 삶에서 다음 선택을 생각해요."; calendar.localRotation=Quaternion.identity; }
        }
        void Restore()
        {
            if(agent!=null) { agent.enabled=true; if(agent.isOnNavMesh) agent.isStopped=wasStopped; }
            if(fly!=null) fly.enabled=wasEnabled;
        }
        void OnDisable() { if(IsPlaying) Restore(); }
    }
}
