using UnityEngine;
using System.Collections;

namespace TheGlitch
{
    public class MirrorRaceManager : MonoBehaviour
    {
        public bool IsRaceActive = false;

        [Header("赛道配置")]
        public WirePuzzleManager PlayerPuzzle; // 拖入左侧玩家的电路板
        public WirePuzzleManager GhostPuzzle;  // 拖入右侧 Ghost 的电路板

        public MirrorGhostAI GhostAI; // 拖入右侧的 Ghost
        public GameObject ExitDoor;   // 胜利后要隐藏的大门

        [Header("UI (可选)")]
        public GameObject LockoutUI; // 输掉时弹出的 "系统锁定 30秒"

        private bool _isLockedOut = false;

        private void OnTriggerEnter(Collider other)
        {
            // 玩家进门触发比赛
            if (other.CompareTag("Player") && !_isLockedOut && !IsRaceActive)
            {
                StartRace();
            }
        }

        public void StartRace()
        {
            IsRaceActive = true;
            Debug.Log("骇客竞速开始！");

            PlayerPuzzle.ResetBoard();
            GhostPuzzle.ResetBoard();

            if (GhostAI) GhostAI.StartHacking(this);
        }

        public void CheckWinCondition()
        {
            if (!IsRaceActive) return;

            if (PlayerPuzzle.IsHacked)
            {
                IsRaceActive = false;
                if (GhostAI) GhostAI.StopHacking();
                Debug.Log("玩家胜利！大门开启！");
                if (ExitDoor) ExitDoor.SetActive(false); // 开门
            }
            else if (GhostPuzzle.IsHacked)
            {
                IsRaceActive = false;
                if (GhostAI) GhostAI.StopHacking();
                Debug.Log("Ghost 胜利！系统被锁定！");
                StartCoroutine(LockoutRoutine());
            }
        }

        private IEnumerator LockoutRoutine()
        {
            _isLockedOut = true;
            if (LockoutUI) LockoutUI.SetActive(true);

            yield return new WaitForSeconds(30f); // 惩罚 30 秒

            _isLockedOut = false;
            if (LockoutUI) LockoutUI.SetActive(false);
            Debug.Log("系统解锁，重新挑战！");

            StartRace();
        }
    }
}