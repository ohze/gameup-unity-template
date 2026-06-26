using UnityEngine;
using GameUp.Core;
using GameUpCore.Runtime.LocalTracking;

public class Example : MonoBehaviour
{
    public PlayerGameMainData playerGameMainData;
    private void Start()
    {
        playerGameMainData = PlayerGameMainData.Create();
        TimeUtils.Initialize();
    }

    [Button]
    public void LevelUp()
    {
        playerGameMainData.LevelUp();
    }

    [Button]
    public void TestAudio()
    {
        AudioManager.PlayAudio(AudioID.Skill_Skeleton_Bomb);
    }

    [Button]
    private void GenerateLevelTracking()
    {
        LocalLevelTracking.Instance.GenerateFakeData();
    }
}