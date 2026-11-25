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
        GamblingPhase spokenPhase;
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
            if(spokenPhase!=Phase)
            {
                spokenPhase=Phase;
                if(Phase!=GamblingPhase.Complete)
                    InfantRoomView.SpeakFor(fly,Phase==GamblingPhase.SmallWin ? "땄다! 한 번만 더 해 볼까?" :
                        Phase==GamblingPhase.PassingMonths ? "이번엔 되겠지. 딱 한 번만 더…" : "생활비도 집도… 다 잃었어.");
            }
            coin.SetActive(Phase==GamblingPhase.SmallWin);
            if(Phase==GamblingPhase.SmallWin) caption.text="당첨";
            else if(Phase==GamblingPhase.PassingMonths)
            {
                float progress=Mathf.Clamp01((elapsed-config.gamblingWinSeconds)/Mathf.Max(.01f,config.gamblingCalendarSeconds));
                CalendarMonth=Mathf.Clamp(1+Mathf.FloorToInt(progress*12),1,12);
                caption.text=CalendarMonth+"개월 경과";
                calendar.localRotation=Quaternion.Euler(0,0,CalendarMonth%2==0 ? 4 : -4);
            }
            else { caption.text="잔액 0"; calendar.localRotation=Quaternion.identity; }
        }
        void Restore()
        {
            if(agent!=null) { agent.enabled=true; if(agent.isOnNavMesh) agent.isStopped=wasStopped; }
            if(fly!=null) fly.enabled=wasEnabled;
        }
        void OnDisable() { if(IsPlaying) Restore(); }
    }
}
