using UnityEngine;
using GameUp.Core;

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
        //AudioManager.PlayAudio(AudioID.Hit_Death);
    }

    [Button]
    public void ShowPopup()
    {
        PopupTest.OpenViewAsync();
    }

    [Button]
    public void ClosePopup()
    {
        PopupTest.CloseView();
    }

    [Button]
    public void ShowScreen()
    {
        ScreenTest.OpenViewAsync();
    }

    [Button]
    public void ShowScreen2()
    {
        ScreenTest2.OpenViewAsync();
    }
}